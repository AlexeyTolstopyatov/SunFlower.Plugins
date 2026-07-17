//
// CoffeeLake (C) 2026-*
//
// The NeFlatImageBuilder.cs builds a flat memory image from NE .CODE segments,
// applies internal relocations to convert far pointers into flat addresses,
// and provides a mapping table for back-translation during disassembly annotation.
//
// Flat addressing scheme:
//     flatAddr = (segmentNumber - 1) * 0x10000 + offsetInSegment
// This gives each segment a 64KB window in linear space.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Text;
using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;

namespace SunFlower.Ne.Services;

/// <summary>
/// Result of building a flat image: the flat binary and translation maps.
/// </summary>
public class FlatImageResult
{
    /// <summary> Flat binary image </summary>
    public byte[] Image { get; init; } = [];

    /// <summary> Total flat size in bytes. </summary>
    public int Size { get; init; }

    /// <summary> Map of flat -> far pointers </summary>
    public Dictionary<int, FlatSource> AddressMap { get; init; } = [];

    /// <summary> For each segment: its base flat address. </summary>
    public Dictionary<uint, int> SegmentBase { get; init; } = [];

    /// <summary> Far Entry points converted to flat pointers. </summary>
    public int[] FlatEntryPoints { get; init; } = [];

    /// <summary> Export entry points converted to flat addresses with symbol names. </summary>
    public Dictionary<int, string> ExportAtFlat { get; init; } = [];

    /// <summary> Import relocations converted to flat addresses. </summary>
    public Dictionary<int, string> ImportAtFlat { get; init; } = [];
}

/// <summary>
/// Source location of a flat address in the original NE segments.
/// </summary>
public class FlatSource
{
    public uint Segment { get; init; }
    public int Offset { get; init; }

    public override string ToString() => $"0x{Segment:X4}:0x{Offset:X4}";
}

public class NeFlatImageBuilder(NeDumpManager dump)
{
    private readonly int _segShift = dump.NeHeader.NE_Alignment == 0
        ? 9
        : dump.NeHeader.NE_Alignment; // alignment shift

    // Flat image state
    private byte[] _image = [];
    private int _imageSize;
    private readonly Dictionary<int, FlatSource> _addressMap = [];
    private readonly Dictionary<uint, int> _segmentBase = [];

    // Entry points
    private readonly List<int> _flatEntryPoints = [];
    private readonly Dictionary<int, string> _exportAtFlat = [];
    private readonly Dictionary<int, string> _importAtFlat = [];

    /// <summary>
    /// Builds the flat image from all .CODE segments.
    /// </summary>
    public FlatImageResult Build()
    {
        var codeSegments = dump.Segments
            .Where(s => s.Type == ".CODE")
            .OrderBy(s => s.SegmentNumber)
            .ToList();

        if (codeSegments.Count == 0)
            return CreateEmptyResult();

        // Calculate flat image size: max segment number * 64KB
        var maxSegNum = codeSegments.Max(s => s.SegmentNumber);
        _imageSize = (int)(maxSegNum * 0x10000);
        _image = new byte[_imageSize];

        // Copy each segment into its 64KB window
        foreach (var segment in codeSegments)
        {
            var baseAddr = (int)((segment.SegmentNumber - 1) * 0x10000);
            _segmentBase[segment.SegmentNumber] = baseAddr;

            var fileLength = (int)segment.FileLength;
            if (fileLength == 0) continue;

            var physicalOffset = (int)segment.FileOffset * (1 << _segShift);

            try
            {
                if (!File.Exists(dump.FilePath))
                    continue;

                using var fs = new FileStream(dump.FilePath, FileMode.Open, FileAccess.Read);
                fs.Position = physicalOffset;
                var segBytes = new byte[fileLength];
                var read = fs.Read(segBytes, 0, fileLength);

                // Copy to flat image
                Array.Copy(segBytes, 0, _image, baseAddr, read);

                // Build address map
                for (int i = 0; i < read; i++)
                {
                    _addressMap[baseAddr + i] = new FlatSource
                    {
                        Segment = segment.SegmentNumber,
                        Offset = i
                    };
                }
            }
            catch
            {
                // Segment data unavailable - skip
            }
        }

        // Apply internal relocations to fix up far pointers
        ApplyInternalRelocations(codeSegments);

        // Convert entry points to flat addresses
        ConvertEntryPoints(codeSegments);

        // Convert import relocations to flat addresses
        ConvertImportRelocations(codeSegments);

        return new FlatImageResult
        {
            Image = _image,
            Size = _imageSize,
            AddressMap = _addressMap,
            SegmentBase = _segmentBase,
            FlatEntryPoints = _flatEntryPoints.Distinct().ToArray(),
            ExportAtFlat = _exportAtFlat,
            ImportAtFlat = _importAtFlat
        };
    }

    private FlatImageResult CreateEmptyResult()
    {
        return new FlatImageResult
        {
            Image = [],
            Size = 0,
            AddressMap = [],
            SegmentBase = [],
            FlatEntryPoints = [],
            ExportAtFlat = [],
            ImportAtFlat = []
        };
    }

    /// <summary>
    /// Applies Internal relocations: patches far pointers in the flat image
    /// so that segment:offset references become flat addresses.
    /// </summary>
    private void ApplyInternalRelocations(List<SegmentModel> codeSegments)
    {
        foreach (var segment in codeSegments)
        {
            if (segment.Relocations.Count == 0)
                continue;

            var baseAddr = _segmentBase.GetValueOrDefault(segment.SegmentNumber, 0);

            foreach (var rel in segment.Relocations)
            {
                if (!rel.RelocationType.Equals("Internal", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rel.AddressType != 3 && rel.AddressType != 11)
                    continue; // Only process far pointers (32-bit or 48-bit)

                // offsetInSegment points to where the far pointer is stored
                var flatAddr = baseAddr + rel.OffsetInSegment;

                // Read the original far pointer from the flat image
                if (flatAddr + 4 > _imageSize)
                    continue;

                var targetSeg = (uint)(_image[flatAddr] | (_image[flatAddr + 1] << 8));
                var targetOff = (ushort)(_image[flatAddr + 2] | (_image[flatAddr + 3] << 8));

                // Convert (targetSeg, targetOff) to flat address
                var targetFlat = TargetToFlat(targetSeg, targetOff);
                if (targetFlat < 0)
                    continue;

                // Patch the far pointer in the flat image
                _image[flatAddr] = (byte)(targetFlat & 0xFF);
                _image[flatAddr + 1] = (byte)((targetFlat >> 8) & 0xFF);
                _image[flatAddr + 2] = (byte)((targetFlat >> 16) & 0xFF);
                _image[flatAddr + 3] = (byte)((targetFlat >> 24) & 0xFF);
            }
        }
    }

    /// <summary>
    /// Converts a NE segment:offset to flat address.
    /// If the target is not a code segment known to us,
    /// returns -1 (cannot convert).
    /// </summary>
    private int TargetToFlat(uint targetSeg, ushort targetOff)
    {
        if (_segmentBase.TryGetValue(targetSeg, out var segBase))
            return segBase + targetOff;

        // Maybe it's a known fixed segment? Check all segments
        var segment = dump.Segments.FirstOrDefault(s => s.SegmentNumber == targetSeg);
        if (segment != null)
        {
            // This segment exists but wasn't put in flat image (e.g., data segment)
            // Return -1, caller will handle
            return -1;
        }

        // Unknown segment -> can't resolve
        return -1;
    }

    /// <summary>
    /// Converts all entry points (from header and entry table) to flat addresses.
    /// </summary>
    private void ConvertEntryPoints(List<SegmentModel> codeSegments)
    {
        // Main entry point from header CS:IP
        var cs = dump.NeHeader.NE_CsIp >> 16;
        var ip = (uint)(dump.NeHeader.NE_CsIp & 0xFFFF);

        var flatCsIp = ConvertSegOffsetToFlat(cs, (int)ip);
        if (flatCsIp >= 0)
            _flatEntryPoints.Add(flatCsIp);

        // Entry table bundles
        foreach (var bundle in dump.EntryBundles)
        {
            foreach (var ep in bundle.EntryPoints)
            {
                if (ep.Type == "[UNUSED]")
                    continue;

                var flatEp = ConvertSegOffsetToFlat(ep.Segment, (int)ep.Offset);
                if (flatEp >= 0)
                {
                    _flatEntryPoints.Add(flatEp);

                    // Check if this is an export
                    if (_exportByName.TryGetValue((int)ep.Ordinal, out var expName))
                    {
                        _exportAtFlat[flatEp] = expName;
                    }
                }
            }
        }

        // Sort entry points
        _flatEntryPoints.Sort();
    }

    // Need to reference _exportByName — store reference
    private Dictionary<int, string> _exportByName = [];

    /// <summary>
    /// Sets the export name table from NeDecoderService.
    /// </summary>
    public void SetExportNames(Dictionary<int, string> exportByName)
    {
        _exportByName = exportByName;
    }

    /// <summary>
    /// Converts import relocations to flat addresses and resolves their symbol names.
    ///
    /// In NE format, an import relocation entry points to the offset of a far pointer
    /// (4 bytes: segment:offset = 0x0000:0xFFFF). The CALLF instruction is:
    ///     0x9A [far_pointer 4 bytes]  (5 bytes total)
    /// The relocation entry's OffsetInSegment points to the far pointer data,
    /// i.e. OffsetInSegment = instruction_offset + 1.
    ///
    /// We store:
    ///   _importAtFlat[flatAddr_of_pointer] = symbol   (maps far pointer location)
    /// </summary>
    private void ConvertImportRelocations(List<SegmentModel> codeSegments)
    {
        // Gather all imported procedures (module -> list of Import)
        var allImports = new List<Import>();
        foreach (var (_, imports) in dump.ImportModels)
            allImports.AddRange(imports);

        foreach (var segment in codeSegments)
        {
            if (segment.Relocations.Count == 0)
                continue;

            var baseAddr = _segmentBase.GetValueOrDefault(segment.SegmentNumber, 0);

            foreach (var rel in segment.Relocations)
            {
                if (!rel.RelocationType.Equals("Import", StringComparison.OrdinalIgnoreCase))
                    continue;

                // The relocation offset points to the far pointer data in the segment
                var flatAddr = baseAddr + rel.OffsetInSegment;

                // Resolve the symbol name
                string? symbol;

                // Import by ordinal
                // Completed condition of being unnamed import: rel.Ordinal > 0
                if (rel.Ordinal > 0)
                {
                    var moduleIdx = rel.ModuleIndex;
                    var matchingImport = allImports.FirstOrDefault(i =>
                        i.ModuleIndex == moduleIdx && i.Ordinal == "@" + rel.Ordinal);

                    symbol = matchingImport != null
                        ? $"{matchingImport.Module}!{matchingImport.Ordinal}"
                        : $"@?{rel.Ordinal}";
                }
                // Import by name
                else if (rel.NameOffset > 0)
                {
                    // Interesting fact: Import procedure names can't have 
                    // same address in the import table. Matching by module name is redundant
                    var matchingImport = allImports.FirstOrDefault(i =>
                        i.NameOffset == rel.NameOffset);

                    symbol = matchingImport != null
                        ? $"{matchingImport.Module}!{matchingImport.Procedure}"
                        : $"<module>!0x{rel.NameOffset:X4}";
                }
                else
                {
                    symbol = null;
                }

                // Symbols are safe now. Don't sanitize strings
                if (symbol is null)
                    continue;

                _importAtFlat[flatAddr] = symbol;

                // Also store by (segment, offsetInSegment) for backward compat
                var segKey = ((int)segment.SegmentNumber << 16) | rel.OffsetInSegment;
                _importAtFlat[-segKey] = symbol;
            }
        }
    }

    private static string SanitizeSymbol(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '.' || c == '$')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "sym" : result;
    }

    /// <summary>
    /// Converts a NE segment:offset pair to a flat address.
    /// Returns -1 if the segment is not a code segment.
    /// </summary>
    private int ConvertSegOffsetToFlat(uint seg, int offset)
    {
        if (_segmentBase.TryGetValue(seg, out var baseAddr))
            return baseAddr + offset;
        return -1;
    }
}