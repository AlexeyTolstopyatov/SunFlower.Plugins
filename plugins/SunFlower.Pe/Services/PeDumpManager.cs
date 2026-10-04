using System.Runtime.InteropServices;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;
using Directory = SunFlower.Pe.Models.Directory;

namespace SunFlower.Pe.Services;

///
/// CoffeeLake 2024-2026
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
///
/// <summary>
/// Orchestrator for the Portable Executable format.
/// The whole image is parsed inside the constructor (same shape as
/// <c>LeDumpManager</c>): fixed headers are read and validated here,
/// while the variable-length tables are delegated to dedicated managers
/// (<see cref="PeDirectoriesManager"/>, <see cref="PeSectionsManager"/>).
/// </summary>
public class PeDumpManager : UnsafeManager
{
    public MzHeader Dos2Header { get; }
    public PeFileHeader FileHeader { get; }
    public PeOptionalHeader32 OptionalHeader32 { get; private set; }
    public PeOptionalHeader OptionalHeader { get; private set; }
    public Directory[] Directories { get; }
    public PeSection[] PeSections { get; }
    public ImageDetails ImageDetails { get; }
    public Vb5Header Vb5Header { get; }
    public Vb4Header Vb4Header { get; }
    public bool Is64Bit { get; private set; }
    public long VbOffset { get; private set; }

    /// <summary> Decoded Microsoft Rich header (compiler/linker fingerprint). </summary>
    public RichHeaderModel RichHeader { get; }

    /// <summary> Total size of the analyzed file in bytes. </summary>
    public long FileLength { get; }

    /// <summary>
    /// Structural inconsistencies collected while parsing (raw data beyond the
    /// file, overlapping sections, entry point outside any section, ...).
    /// The dump never throws on those; they are reported here instead.
    /// </summary>
    public IReadOnlyList<string> Anomalies => _anomalies;
    private readonly List<string> _anomalies = [];

    // Size of the fixed part of the optional header (before the DataDirectory table).
    private const int Header32Size = 96;
    private const int Header64Size = 112;

    private const int MaxSections = 96;
    private const int MaxDirectories = 16;

    private readonly uint _peOffset;

    public PeDumpManager(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        FileLength = stream.Length;

        Dos2Header = Fill<MzHeader>(reader);
        if (Dos2Header.e_sign is not 0x5a4d and not 0x4d5a)
            throw new NotSupportedException("Doesn't have DOS/2 signature");

        _peOffset = Dos2Header.e_lfanew;
        stream.Position = _peOffset;
        if (reader.ReadUInt32() != 0x00004550)
            throw new NotSupportedException("Doesn't have 'PE' signature");

        FileHeader = Fill<PeFileHeader>(reader);
        // Bitness is taken from the Optional Header magic, not from the
        // IMAGE_FILE_32BIT_MACHINE flag which is unreliable in odd images.
        var optionalOffset = stream.Position;
        var magic = reader.ReadUInt16();
        stream.Position = optionalOffset;

        long fixedOptionalSize;
        switch (magic)
        {
            case 0x010B: // PE32
                var header32 = Fill<PeOptionalHeader32>(reader);
                OptionalHeader32 = header32;
                OptionalHeader = PeOptionalHeader32.Into(ref header32);
                Is64Bit = false;
                fixedOptionalSize = Header32Size;
                break;
            case 0x020B: // PE32+
                OptionalHeader = Fill<PeOptionalHeader>(reader);
                Is64Bit = true;
                fixedOptionalSize = Header64Size;
                break;
            case 0x0107: 
                Console.WriteLine("PE ROM?");
                throw new NotSupportedException("Unbelievable image. Support is coming soon");
            default:
                throw new NotSupportedException($"Unknown optional header magic 0x{magic:X4}");
        }
        // Data Directory table: NumberOfRvaAndSizes but never exceed 16.
        var declaredDirectories = OptionalHeader.NumberOfRvaAndSizes;
        var directoryCount = declaredDirectories > MaxDirectories
            ? MaxDirectories
            : declaredDirectories;
        var readDirectories = new PeDirectoriesManager(
            reader, 
            optionalOffset + fixedOptionalSize, 
            directoryCount
        ).Directories;
        // Keep the classic 16-slot table so fixed indices ([0], [1], [12], [14])
        // stay valid for any consumer even when fewer directories are declared.
        Directories = readDirectories;
        //Array.Copy(readDirectories, Directories, directoryCount);
        
        // Section table: offset is derived from SizeOfOptionalHeader rather than
        // from the reader position, so truncated optional headers are handled.
        var sectionTableOffset = _peOffset + 4
            + Marshal.SizeOf(typeof(PeFileHeader))
            + FileHeader.SizeOfOptionalHeader;
        var sectionCount = FileHeader.NumberOfSections;
        if (sectionCount > MaxSections)
            sectionCount = MaxSections;
        PeSections = new PeSectionsManager(reader, sectionTableOffset, sectionCount).Sections;
        // Non-throwing structural check: packed / crafted images are reported
        // through Anomalies instead of aborting the whole dump.
        AnalyzeImage(sectionTableOffset, sectionCount, OptionalHeader);

        ImageDetails = new ImageDetails
        {
            Is64Bit = Is64Bit,
            NumberOfSections = FileHeader.NumberOfSections,
            NumberOfRva = declaredDirectories,
            SectionAlignment = OptionalHeader.SectionAlignment,
            FileAlignment = OptionalHeader.FileAlignment,
            // ImageDetails keeps a 32-bit model (consumed by the VB
            // managers): for PE32+ the low 32 bits of ImageBase are kept, and
            // BaseOfData does not exist in PE32+ (defaults to 0).
            ImageBase = (uint)OptionalHeader.ImageBase,
            BaseOfCode = OptionalHeader.BaseOfCode,
            BaseOfData = Is64Bit ? 0u : OptionalHeader32.BaseOfData,
            Sections = PeSections,
            Directories = Directories,
            EntryPoint = OptionalHeader.AddressOfEntryPoint
        };
        // The Rich header lives in the DOS stub; it is parsed from its own
        // stream so the sequential reader state below stays untouched.
        RichHeader = new RichManager(path, _peOffset).RichHeader;
        // VB runtimes stay untouched: their managers are still fed the shared
        // section info and the live reader, exactly like before the refactor.
        var vb5Runtime = new PeVbRuntime56Manager(ImageDetails, reader);
        var vb4Runtime = new PeVbRuntime4Manager(ImageDetails, reader);

        Vb5Header = vb5Runtime.Vb5Header;
        Vb4Header = vb4Runtime.Vb4Header;

        if (vb5Runtime.Vb5Header.VbMagic == "VB5!".ToCharArray())
            VbOffset = vb5Runtime.VbOffset;

        if (vb4Runtime.Vb4Header.Signature == "\x81\x35\x54\xB6".ToCharArray())
            VbOffset = vb4Runtime.VbOffset;
    }

    /// <summary>
    /// Collects structural anomalies without throwing: crafted / packed images
    /// are exactly the ones a reverse engineer cares about, so instead of
    /// aborting we record what looks off.
    /// </summary>
    private void AnalyzeImage(long sectionTableOffset, uint sectionCount, PeOptionalHeader optional)
    {
        var sizeOfHeaders = optional.SizeOfHeaders;
        var sectionAlignment = optional.SectionAlignment;
        var fileAlignment = optional.FileAlignment;

        // The headers region must at least cover the section table.
        var expectedHeadersEnd = sectionTableOffset
            + sectionCount * Marshal.SizeOf(typeof(PeSection));
        if (sizeOfHeaders < expectedHeadersEnd)
        {
            _anomalies.Add(
                $"SizeOfHeaders (0x{sizeOfHeaders:X}) is smaller than the section table end (0x{expectedHeadersEnd:X})");
        }

        foreach (var section in PeSections)
        {
            var name = new string(section.Name).TrimEnd('\0');

            // Raw data must fit inside the file.
            if (section.SizeOfRawData == 0)
                continue;

            var rawEnd = (long)section.PointerToRawData + section.SizeOfRawData;
            if (rawEnd > FileLength)
            {
                _anomalies.Add(
                    $"[{name}]: raw data [0x{section.PointerToRawData:X}..0x{rawEnd:X}) exceeds the file size (0x{FileLength:X})");
            }

            if (fileAlignment != 0 && section.PointerToRawData % fileAlignment != 0)
            {
                _anomalies.Add(
                    $"[{name}]: PointerToRawData (0x{section.PointerToRawData:X}) is not FileAlignment (0x{fileAlignment:X}) aligned");
            }
        }

        // Mapped addresses are expected to honor SectionAlignment.
        if (sectionAlignment != 0)
        {
            foreach (var section in PeSections)
            {
                if (section.VirtualAddress != 0 && section.VirtualAddress % sectionAlignment != 0)
                {
                    var name = new string(section.Name).TrimEnd('\0');
                    _anomalies.Add(
                        $"[{name}]: VirtualAddress (0x{section.VirtualAddress:X}) is not SectionAlignment (0x{sectionAlignment:X}) aligned");
                }
            }
        }

        // Overlapping raw ranges are a packer / crafted-image fingerprint.
        var ordered = PeSections
            .Where(s => s.SizeOfRawData != 0)
            .OrderBy(s => s.PointerToRawData)
            .ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            var previousEnd = (long)ordered[i - 1].PointerToRawData + ordered[i - 1].SizeOfRawData;
            if (ordered[i].PointerToRawData >= previousEnd) 
                continue;
            
            var previousName = new string(ordered[i - 1].Name).TrimEnd('\0');
            var currentName = new string(ordered[i].Name).TrimEnd('\0');
            
            _anomalies.Add($"Sections '{previousName}' and '{currentName}' overlap in raw space");
        }

        // Entry point must land inside some section.
        var entryPoint = optional.AddressOfEntryPoint;
        if (entryPoint != 0 && !PeSections.Any(s => WithinSection(s, entryPoint)))
            _anomalies.Add($"AddressOfEntryPoint (0x{entryPoint:X}) does not belong to any section");

        // SizeOfImage should cover the headers and every mapped file section.
        if (sectionAlignment != 0)
        {
            var expectedImageEnd = AlignUp(sizeOfHeaders, sectionAlignment);
            foreach (var section in PeSections)
            {
                var span = section.VirtualSize > section.SizeOfRawData
                    ? section.VirtualSize
                    : section.SizeOfRawData;
                var end = section.VirtualAddress + AlignUp(span, sectionAlignment);
                if (end > expectedImageEnd)
                    expectedImageEnd = end;
            }

            if (optional.SizeOfImage < expectedImageEnd)
            {
                _anomalies.Add(
                    $"SizeOfImage (0x{optional.SizeOfImage:X}) is smaller than the mapped image end (0x{expectedImageEnd:X})");
            }
        }

        // Trailing data after the last section (overlay): could be an
        // Authenticode signature, an installer, or a packed payload.
        var lastSectionEnd = (
            from section in PeSections 
            where section.SizeOfRawData != 0 
            select (long)section.PointerToRawData + section.SizeOfRawData
        )
            .Prepend(0)
            .Max();

        if (lastSectionEnd > 0 && lastSectionEnd < FileLength)
        {
            _anomalies.Add(
                $"Overlay of 0x{FileLength - lastSectionEnd:X} bytes after the last section (may be an Authenticode signature or embedded data)");
        }
    }

    /// <summary> Rounds <paramref name="value"/> up to a multiple of <paramref name="alignment"/>. </summary>
    private static ulong AlignUp(ulong value, uint alignment)
        => alignment == 0 ? value : (value + alignment - 1) / alignment * alignment;

    /// <summary>
    /// Mirrors <see cref="DirectoryManager"/> span resolution:
    /// a section spans max(VirtualSize, SizeOfRawData) bytes.
    /// </summary>
    private static bool WithinSection(PeSection section, uint rva)
    {
        var span = section.VirtualSize > section.SizeOfRawData
            ? section.VirtualSize
            : section.SizeOfRawData;
        if (span == 0)
            return false;

        var start = (ulong)section.VirtualAddress;
        return rva >= start && rva < start + span;
    }
}