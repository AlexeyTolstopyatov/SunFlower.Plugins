//
// CoffeeLake (C) 2026-*
//
// Be ready to see it. This is a real shit u know... 
// But it FINALLY WORKS fine and IDA 8.3 tells this the relocation matches right.
// 

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sunflower.Dasm;
using SunFlower.Le.Headers;
using Object = SunFlower.Le.Headers.Le.Object;

namespace SunFlower.Le.Services;
public partial class LeDecoderService
{
    private readonly LeDumpManager _dump;
    private readonly string _filePath;

    /// <summary>
    /// Export symbols map
    /// Far location as a key and symbol as a value
    /// </summary>
    private readonly Dictionary<(int obj, int off), string> _exportAt = [];

    /// <summary>
    /// Fixup symbols map
    /// Far location as a key and symbol tuple as a value (with applied additive)  
    /// </summary>
    private readonly Dictionary<(int obj, int off), (string symbol, int pageNumber)> _fixupSymbolAt = [];

    /// <summary>
    /// Import Procedure Names from the import procedure table. Key is a NameOffset 
    /// </summary>
    private ImportRecord[] _importRecords = [];

    private List<string> _results = [];
    private readonly int _mainObj;
    private readonly uint _mainEip;
    private readonly int _pageSize;

    /// <summary>
    /// Maximum count of the analysis passes per object. Every pass disassembles
    /// the object again, but the far addresses (CALLF/JMPF immediates) found
    /// during the previous pass are added to the entry points, so the code
    /// behind the big addresses is analysed as well.
    /// </summary>
    private const int MaxAnalysisPasses = 8;

    /// <summary>
    /// Entry points which the code references through the internal fixups.
    /// Key is a 1-based object number, value is a set of offsets inside it.
    /// </summary>
    private readonly Dictionary<int, SortedSet<int>> _fixupTargetsByObject = [];

    /// <summary>
    /// Analyse the objects which are not marked as executable. A data object may
    /// keep procedures too: drivers place their DDB and the procedure pointers
    /// into .DATA, applications keep callbacks there, so every location which the
    /// code references through the internal fixups is analysed as well.
    /// Turn it off to keep such objects as plain data.
    /// </summary>
    public bool AnalyseDataObjects { get; set; } = true;

    /// <summary>
    /// Dump the printable runs of a data object as DB string literals. The raw
    /// bytes are shielded by <see cref="SunFlower.Abstractions.FlowerReport.SafeString"/>
    /// before they land in the listing.
    /// Turn it off to skip the string scan.
    /// </summary>
    public bool DumpStrings { get; set; } = true;

    /// <summary>
    /// Minimal length of a printable run reported as a string.
    /// </summary>
    public int MinStringLength { get; set; } = DataObjectExtension.DefaultMinStringLength;

    /// <summary>
    /// A data object may keep its procedures without any reference from the code
    /// (the OS installs them through a table, the runtime builds the pointer).
    /// Such a procedure is spotted by its frame prologue and becomes an entry
    /// point, so the code hidden in the data is disassembled too.
    /// Turn it off to analyse only the locations referenced by the fixups.
    /// </summary>
    public bool DiscoverDataProcedures { get; set; } = true;

    public LeDecoderService(string filePath, LeDumpManager dump)
    {
        _filePath = filePath;
        _dump = dump;
        _mainObj = (int)_dump.LeHeader.e32_startobj;
        _mainEip = _dump.LeHeader.e32_eip;
        _pageSize = (int)_dump.LeHeader.e32_pagesize;
        // _pageShift = (int)_dump.LeHeader.e32_pageshift;
        // long fileOffset = dataPageOffset + ((long)page.PageDataOffset << _pageShift);
    }

    public string[] Disassemble()
    {
        var moduleName = _dump.ResidentNames.Length > 0
            ? _dump.ResidentNames[0].String
            : "<unknown_module>";

        _results.AddRange([
            $"; SunFlower.LE.dll for Linear Executable {moduleName} (bases on Microsoft LE v.0:32)",
            $"; Objects: {_dump.Objects.Length}, ({_dump.Pages.Length} pages)",
            $"; Entry Bundles: {_dump.EntryBundles.Length}",
            $"; Relocations: {_dump.FixupRecords.Length}",
            $"; Imports: {_dump.ImportRecords.Length}",
            ";"
        ]);
        
        _results.AddRange(ExternalEntryPointsExtension.AddSignatures(_dump.EntryBundles
            .SelectMany(x => x.Entries)
            .ToArray()));
        
        BuildExportMap();

        _importRecords = _dump.ImportRecords;
        
        //BuildImportNameMap();
        BuildFixupSymbolMap();
        
        for (var i = 0; i < _dump.Objects.Length; i++)
        {
            var obj = _dump.Objects[i];
            if (obj.VirtualSegmentSize == 0) continue;

            // Data objects are analysed too: they may keep procedures
            // (procedure pointers of the drivers DDB, callbacks, jump tables)
            if (!obj.Execute && !AnalyseDataObjects) continue;

            TranslateObject(i + 1, obj);
        }

        DescribePseudocode();

        return _results.ToArray();
    }
    /// <summary>
    /// Resolves the immediate operands of the listing using the relocation
    /// records (the highest priority) and the exported procedure names (the last
    /// resort). Every produced line is visited, so a call, a jump, a pushed
    /// pointer and a moved address are resolved with the same rules.
    /// </summary>
    private void DescribePseudocode()
    {
        var lines = _results.SelectMany(s => s.Split('\n')).ToArray();
        var modified = new List<string>(lines.Length);

        foreach (var resultLine in lines)
            modified.Add(DescribeInstruction(resultLine.TrimEnd('\r')));

        _results = modified;
    }

    /// <summary>
    /// Resolves the address immediate of a single instruction line:
    ///     1. the relocation record of the same file position resolves the
    ///        operand exactly (the record keeps the target object and offset);
    ///     2. an exported name is a guess only: the file keeps a bare offset
    ///        inside the address, so the substitution is made when it can not
    ///        be wrong - the offset is not empty and belongs to a single object.
    /// </summary>
    private string DescribeInstruction(string line)
    {
        var match = InstructionPattern().Match(line);

        if (!match.Success)
            return line;

        var instructionObject = Convert.ToInt32(match.Groups["instrObject"].Value);
        var instructionOffset = Convert.ToInt32(match.Groups["instrOffset"].Value, 16);
        var address = match.Groups["addr"].Value;

        // The instruction bytes are kept in the debug annotation, so the operand
        // and the relocation record are bound by the value they share
        var instructionBytes = ParseInstructionBytes(line, match.Groups["instr"].Index + match.Groups["instr"].Length);
        var addressBytes = ParseOperandBytes(address);

        var resolved = ApplyFixup(line, instructionObject, instructionOffset, address, addressBytes, instructionBytes);

        if (resolved is not null)
            return resolved;

        // The exported name is substituted for the pointer transfers only: a data
        // immediate may coincide with an entry point offset just by accident
        if (!IsPointerTransfer(match.Groups["mnemonic"].Value))
            return line;

        var targetOffset = FarTargetOffset(match);

        // An empty address is not a target: every object starts at the zero
        // offset, so the guess would be random. A non-zero offset which belongs
        // to several objects is ambiguous as well
        if (targetOffset <= 0)
            return line;

        var exports = _exportAt.Where(x => x.Key.off == targetOffset).ToArray();

        return exports.Length == 1
            ? ReplaceOperand(line, address, $"::{exports[0].Value}")
            : line;
    }

    /// <summary>
    /// Replaces the first occurrence of the address token inside the operand part
    /// of the instruction. The debug annotation behind the semicolon keeps the
    /// same hexadecimal numbers as the operands, so it must not be corrupted.
    /// </summary>
    private static string ReplaceOperand(string line, string token, string replacement)
    {
        var separator = line.IndexOf(';');
        var body = separator >= 0 ? line[..separator] : line;
        var annotation = separator >= 0 ? line[separator..] : string.Empty;

        var position = body.IndexOf(token, StringComparison.Ordinal);

        if (position < 0)
            return line;

        return body[..position] + replacement + body[(position + token.Length)..] + annotation;
    }

    /// <summary>
    /// Applies a relocation record of the instruction. The record keeps the exact
    /// object-relative offset of the field it relocates, so the whole instruction
    /// range is scanned - not the immediate position only, because a MOV with a
    /// memory operand keeps its immediate behind the ModRM/displacement bytes too.
    /// The record is bound to the operand only when the bytes it points at form
    /// the same value the operand is printed with, so a wrong substitution is not
    /// possible.
    /// </summary>
    private string? ApplyFixup(
        string line,
        int instructionObject,
        int instructionOffset,
        string address,
        byte[] addressBytes,
        byte[] instructionBytes)
    {
        if (addressBytes.Length == 0 || addressBytes.Length > instructionBytes.Length)
            return null;

        for (var delta = 0; delta + addressBytes.Length <= instructionBytes.Length; delta++)
        {
            if (!_fixupSymbolAt.TryGetValue((instructionObject, instructionOffset + delta), out var fixup))
                continue;

            if (instructionBytes.AsSpan(delta, addressBytes.Length).SequenceEqual(addressBytes))
                return ReplaceOperand(line, address, fixup.symbol);
        }

        return null;
    }

    /// <summary>
    /// The raw bytes of a hexadecimal operand as the file keeps them. A 16:16 far
    /// pointer is printed as <c>0xSEG:0xOFF</c> while the offset half is stored
    /// first, so both halves are joined back in that order.
    /// </summary>
    private static byte[] ParseOperandBytes(string address)
    {
        var halves = address.Split(':', StringSplitOptions.RemoveEmptyEntries);

        return halves.Length == 2
            ? [.. ParseLittleEndianHex(halves[1]), .. ParseLittleEndianHex(halves[0])]
            : ParseLittleEndianHex(address);
    }

    /// <summary>
    /// Splits the printed value into the bytes the file keeps:
    ///     <c>0x1234</c> -> <c>0x34, 0x12</c>
    /// </summary>
    private static byte[] ParseLittleEndianHex(string token)
    {
        var digits = token.StartsWith("0x", StringComparison.Ordinal) ? token[2..] : token;

        if (digits.Length == 0 || (digits.Length & 1) != 0)
            return [];

        var result = new byte[digits.Length / 2];

        for (var i = 0; i < result.Length; i++)
        {
            if (!byte.TryParse(digits.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return [];

            result[i] = value;
        }

        Array.Reverse(result);

        return result;
    }

    /// <summary>
    /// Reads the instruction bytes out of the debug annotation:
    ///     <c>; 1:0x0017 B8 34 12</c>
    /// The bytes are the space separated hexadecimal pairs behind the address of
    /// the instruction, up to the first token which is not a pair.
    /// </summary>
    private static byte[] ParseInstructionBytes(string line, int start)
    {
        var result = new List<byte>();
        var index = start;

        while (index < line.Length && line[index] == ' ')
            index++;

        while (index + 2 <= line.Length && IsHexPair(line, index))
        {
            // the pair is a byte only when what follows separates it from the rest
            if (index + 2 < line.Length && line[index + 2] != ' ')
                break;

            result.Add(Convert.ToByte(line.Substring(index, 2), 16));
            index += 2;

            while (index < line.Length && line[index] == ' ')
                index++;
        }

        return result.ToArray();
    }

    private static bool IsHexPair(string line, int index) =>
        char.IsAsciiHexDigit(line[index]) && char.IsAsciiHexDigit(line[index + 1]);

    /// <summary>
    /// Tells if the instruction loads or transfers a pointer (a call, a jump or a
    /// stack push/pop) so its operand might be an exported address.
    /// </summary>
    private static bool IsPointerTransfer(string mnemonic) =>
        mnemonic is "CALLF" or "JMPF" or "CALL" or "JMP" or "POP" or "PUSH";

    private void BuildExportMap()
    {
        var ordinal = 1;
        foreach (var bundle in _dump.EntryBundles)
        {
            if (bundle.Count == 0)
            {
                ordinal++;
                continue;
            }

            var objNum = bundle.ObjectNumber;

            foreach (var entry in bundle.Entries)
            {
                if (entry is EntryUnused or EntryForwarder)
                {
                    ordinal++;
                    continue;
                }

                (int offset, string name) pair = entry switch
                {
                    Entry16Bit e16 => (e16.Offset, e16.EntryName),
                    Entry32Bit e32 => ((int)e32.Offset, e32.EntryName),
                    Entry286CallGate eg => (eg.Offset, eg.EntryName),
                    _ => (-1, "<unbelievable entry point>")
                };
                if (string.IsNullOrEmpty(pair.name))
                    pair.name = $"_@{entry.Ordinal}";
                
                if (pair.offset >= 0 && objNum > 0)
                    _exportAt[(objNum, pair.offset)] = FailSafe(pair.name);

                ordinal++;
            }
        }
    }

    /// <summary>
    /// Remove unexpected characters from the string
    /// </summary>
    private static string FailSafe(string s)
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

    private void BuildFixupSymbolMap()
    {
        var fpo = _dump.FixupPageOffsets;
        if (fpo.Length < 2) return;

        var logicalPageToOwner = new Dictionary<int, (int objNum, int pageIdx)>();
        for (var oi = 0; oi < _dump.Objects.Length; oi++)
        {
            var obj = _dump.Objects[oi];
            
            if (obj.PageMapEntries == 0) 
                continue;
            
            var startLogical = (int)obj.PageMapIndex; // (int)_dump.Pages[obj.PageMapIndex - 1].Page.LongPageIndex; // ; // 0-based
            for (var pi = 0; pi < obj.PageMapEntries; pi++)
            {
                var logicalPage = startLogical + pi; // +0 +1 +2 ...
                logicalPageToOwner[logicalPage] = (oi + 1, pi);
                
                Console.WriteLine($"lpag#={logicalPage} -> (obj#{oi + 1} page[{pi}])");
            }
        }

        uint curOffset = 0;

        foreach (var fixup in _dump.FixupRecords)
        {
            // while (currentLogicalPage < fpo.Length - 1 && curOffset >= fpo[currentLogicalPage + 1].Offset)
            //     currentLogicalPage++;

            if (!logicalPageToOwner.TryGetValue(fixup.LogicalPage, out var owner))
            {
                AdvancePosition(ref curOffset, fixup);
                continue;
            }

            var (objNum, pageIdx) = owner;
            var offsets = fixup.SourceType.HasSourceList ? fixup.SourceOffsetList : [fixup.SourceOffset];
            var symbol = ResolveFixupSymbol(fixup);

            RememberCodeReference(objNum, fixup);

            if (symbol != null)
            {
                foreach (var srcOff in offsets)
                {
                    var objOffset = pageIdx * _pageSize + srcOff;
                    if (_fixupSymbolAt.ContainsKey((objNum, objOffset)))
                        continue;

                    //var size = fixup.RelocationFlags.Is32BitTargetOffset ? 4 : 2;
                    _fixupSymbolAt[(objNum, objOffset)] = (symbol, fixup.LogicalPage);
                }
            }

            AdvancePosition(ref curOffset, fixup);
        }
    }

    /// <summary>
    /// A data object may keep procedures too: drivers place their DDB and the
    /// procedure pointers into .DATA, applications keep callbacks there. So every
    /// internal fixup which is made by the code and points into an object is
    /// remembered as a candidate entry point of that object.
    /// </summary>
    private void RememberCodeReference(int sourceObject, LeFixupRecord fixup)
    {
        if (fixup.TargetData is not LeFixupTargetInternal target)
            return;

        if (sourceObject < 1 || sourceObject > _dump.Objects.Length)
            return;

        if (!_dump.Objects[sourceObject - 1].Execute)
            return;

        if (!IsCodePointerAddressType(fixup.SourceType.AddressType))
            return;

        var targetObject = target.ObjectNumber;
        var targetOffset = (long)target.TargetOffset + (fixup.AdditiveValue ?? 0);

        if (targetObject < 1 || targetObject > _dump.Objects.Length)
            return;

        if (targetOffset <= 0 || targetOffset > int.MaxValue)
            return;

        if (!_fixupTargetsByObject.TryGetValue(targetObject, out var offsets))
        {
            offsets = [];
            _fixupTargetsByObject[targetObject] = offsets;
        }

        offsets.Add((int)targetOffset);
    }

    /// <summary>
    /// Tells if the fixup address type stores a full far pointer inside the object
    /// (16:32/16:48), i.e. the relocated source may be a procedure pointer of a
    /// table or of a structure (DDB, callbacks). A bare 16/32-bit offset is kept
    /// out: it is a data reference in most of the cases.
    /// </summary>
    private static bool IsCodePointerAddressType(LeFixupAddressType type) =>
        type is LeFixupAddressType.Far32
             or LeFixupAddressType.Far48;

    private static void AdvancePosition(ref uint pos, LeFixupRecord fixup)
    {
        pos += 2; // [Atp|Rtp|...
        pos += (uint)(fixup.SourceType.HasSourceList ? 1 : 2); // [Src/Cnt]

        var flags = fixup.RelocationFlags;

        switch (flags.RelocationType)
        {
            case LeFixupRelocationType.Internal:
                pos += (uint)(flags.Is16BitObjectModule ? 2 : 1);
                // Target offset exists for most types except 16-bit selector (address type = 2)
                if (fixup.SourceType.AddressType != LeFixupAddressType.Selector16)
                    pos += (uint)(flags.Is32BitTargetOffset ? 4 : 2);

                break;
            case LeFixupRelocationType.ImportOrdinal:
                pos += (uint)(flags.Is16BitObjectModule ? 2 : 1);
                pos += (uint)(flags.Is8BitImportOrdinal ? 1 : flags.Is32BitTargetOffset ? 4 : 2);
                break;
            case LeFixupRelocationType.ImportName:
                pos += (uint)(flags.Is16BitObjectModule ? 2 : 1);
                pos += (uint)(flags.Is32BitTargetOffset ? 4 : 2);
                break;
            case LeFixupRelocationType.OsFixup:
                pos += (uint)(flags.Is16BitObjectModule ? 2 : 1);
                break;
        }

        if (flags.HasAdditive)
            pos += (uint)(flags.Is32BitAdditive ? 4 : 2);
        if (fixup.SourceType.HasSourceList)
            pos += (uint)(fixup.SourceOffsetList.Length * 2);
    }

    private string? ResolveFixupSymbol(LeFixupRecord fixup)
    {
        switch (fixup.TargetData)
        {
            case LeFixupTargetInternal target:
                int targetObj = target.ObjectNumber;
                var targetOff = (int)target.TargetOffset;
                var exp = _exportAt.GetValueOrDefault((targetObj, targetOff));

                return
                    exp ?? $"::{targetObj:X4}:{targetOff:X4}";

            case LeFixupTargetImportOrdinal impOrd:
                var modName = GetModuleName(impOrd.ModuleIndex);
                var anon = $"{modName}::@{impOrd.ImportOrdinal}";
                return anon;

            case LeFixupTargetImportName impName:
                var mod = GetModuleName(impName.ModuleIndex);
                var proc = GetProcedureName(impName.NameOffset);
                var name = string.IsNullOrEmpty(proc)
                    ? $"{mod}::+0x{impName.NameOffset:X4}"
                    : $"{mod}::{proc}";

                return name;

            default:
                return null;
        }
    }

    private string GetModuleName(ushort moduleIndex)
    {
        var idx = moduleIndex - 1;
        if (idx >= 0 && idx < _dump.ImportRecords.Length)
            return _dump.ImportRecords[idx].DllName;

        return $"mod_{moduleIndex}";
    }

    private string GetProcedureName(uint nameOffset)
    {
        return _importRecords.First(x => x.Offset == nameOffset).Name;
    }

    private byte[]? BuildObjectBytesByPageIndex(Object obj)
    {
        if (obj.PageMapEntries == 0) return null;

        var startLogical = (int)obj.PageMapIndex - 1;
        if (startLogical < 0) return null;

        var pagesToRead = (int)obj.PageMapEntries;
        var pagesAvail = _dump.Pages.Length - startLogical;
        if (pagesToRead > pagesAvail) pagesToRead = pagesAvail;
        if (pagesToRead <= 0) return null;

        var totalSize = pagesToRead * _pageSize;
        var buf = new byte[totalSize];

        using var fs = File.OpenRead(_filePath);
        using var reader = new BinaryReader(fs);

        for (var i = 0; i < pagesToRead; i++)
        {
            var modelPage = _dump.Pages[startLogical + i];
            var page = modelPage.Page;
            var rawFlags = page.Flags;
            var pageType = (byte)((byte)rawFlags & 0x03);

            if (pageType is not (0 or 3))
                continue;

            var fileOffset = _dump.LeHeader.e32_datapage + (page.LongPageIndex - 1) * _pageSize;
            
            var isLastPage = ((byte)rawFlags & 0x80) != 0;

            var bytesToRead = isLastPage ? (int)_dump.LeHeader.e32_lastpagesize : _pageSize;
            if (bytesToRead <= 0) bytesToRead = _pageSize;

            try
            {
                reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
                var pageBytes = new byte[bytesToRead];
                var read = reader.Read(pageBytes, 0, bytesToRead);
                var destOff = i * _pageSize;
                var copyLen = Math.Min(read, totalSize - destOff);
                if (copyLen > 0)
                    Array.Copy(pageBytes, 0, buf, destOff, copyLen);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[#{nameof(BuildObjectBytesByPageIndex)}]: {e}");
            }
        }

        return buf;
    }

    private void TranslateObject(int objectNumber, Object obj)
    {
        var is32Bit = (obj.ObjectFlagsMask & 0x2000) != 0;
        var modeLabel = is32Bit ? "32-bit" : "16-bit";
        var suggestedName = Object.GetSuggestedNameByPermissions(obj);
        var isExecutable = obj.Execute;

        var objBytes = BuildObjectBytesByPageIndex(obj);
        if (objBytes == null || objBytes.Length == 0) return;

        var entryPoints = new SortedSet<int>();
        
        if (objectNumber == _mainObj)
            entryPoints.Add((int)_mainEip);

        foreach (var bundle in _dump.EntryBundles)
        {
            if (bundle.ObjectNumber != objectNumber) continue;
            foreach (var entry in bundle.Entries.Where(e => e is not EntryUnused))
            {
                var off = entry switch
                {
                    Entry16Bit e16 => e16.Offset,
                    Entry32Bit e32 => (int)e32.Offset,
                    Entry286CallGate eg => eg.Offset,
                    _ => -1 // unreachable address I suppose
                };
                if (off >= 0 && off < objBytes.Length)
                    entryPoints.Add(off);
            }
        }

        // A data object may keep procedures too, so the internal fixup targets
        // referenced by the code (callbacks, DDB procedure fields, jump tables)
        // are used as entry points of such an object
        if (!isExecutable && _fixupTargetsByObject.TryGetValue(objectNumber, out var codeReferences))
        {
            foreach (var reference in codeReferences.Where(r => r >= 0 && r < objBytes.Length))
                entryPoints.Add(reference);
        }

        // The procedure which nobody references directly (the OS installs it
        // through a table, the runtime builds the pointer) leaves the frame
        // prologue as the only trace. The whole object is scanned for it, so the
        // code hidden in the data becomes an entry point as well
        var prologues = 0;
        if (!isExecutable && DiscoverDataProcedures)
        {
            foreach (var prologue in DataObjectExtension.FindProcedurePrologues(objBytes))
            {
                if (entryPoints.Add(prologue))
                    prologues++;
            }
        }

        if (entryPoints.Count == 0 && !isExecutable)
        {
            _results.Add("");
            _results.Add($"; === Object#{objectNumber} : {suggestedName} [{string.Join(", ", obj.ObjectFlags)}] ===");
            _results.Add($";     {modeLabel}, Virtual Size: {obj.VirtualSegmentSize} bytes");
            _results.Add(";     No code references into this object, kept as data");
            DumpObjectStrings(objectNumber, objBytes);
            return;
        }

        if (entryPoints.Count == 0) entryPoints.Add(0);

        var followedTargets = 0;
        string annotation;

        try
        {
            annotation = string.Empty;

            for (var pass = 0; pass < MaxAnalysisPasses; pass++)
            {
                var disassembly = is32Bit
                    ? I80386Decoder.decodeRecursive("", objBytes, [.. entryPoints])
                    : I80286Decoder.decodeRecursive("", objBytes, [.. entryPoints]);

                var farTargets = new List<int>();
                annotation = DescribeFragment(disassembly, objectNumber, farTargets);

                // Far addresses which point inside the object become the new
                // entry points, so procedures behind the big addresses are
                // analyzed too. The loop stops when nothing new is discovered
                var discovered = farTargets
                    .Count(t => t > 0 && t < objBytes.Length && entryPoints.Add(t));

                followedTargets += discovered;

                if (discovered == 0)
                    break;
            }
        }
        catch (Exception ex)
        {
            annotation = $"; Fatal: {ex.Message}";
        }

        _results.Add("");
        _results.Add($"; === Object#{objectNumber} : {suggestedName} [{string.Join(", ", obj.ObjectFlags)}] ===");
        _results.Add($";     {modeLabel}, Virtual Size: {obj.VirtualSegmentSize} bytes, Entry points: {entryPoints.Count}");

        if (!isExecutable)
            _results.Add(";     $Feature is UNDER CONSTRUCTION$");

        if (prologues > 0)
            _results.Add($";     Procedure prologues found: {prologues}");

        if (followedTargets > 0)
            _results.Add($";     Far addresses followed: {followedTargets} more entry point(s)");

        _results.Add(annotation);

        //if (!isExecutable) INTERESTING
        DumpObjectStrings(objectNumber, objBytes);
    }

    /// <summary>
    /// Reports the printable runs of a data object as DB pseudo-instructions.
    /// The strings of the executable objects are not scanned: the code bytes
    /// give too many false positives, and the code is already disassembled.
    /// </summary>
    private void DumpObjectStrings(int objectNumber, byte[] objBytes)
    {
        if (!DumpStrings) 
            return;

        var strings = DataObjectExtension.CollectAsciiStrings(objBytes, MinStringLength);
        if (strings.Count == 0) return;

        _results.Add("");
        _results.Add($"; --- ASCII strings of Object#{objectNumber} ({strings.Count}) ---");

        foreach (var (offset, text) in strings)
        {
            var literal = "DB " + SunFlower.Abstractions.FlowerReport.SafeString(text);
            _results.Add($"\t{literal, -40} ; {objectNumber}:0x{offset:X4} len={text.Length} bytes");
        }
    }

    private string DescribeFragment(string disassembly, int objectNumber, ICollection<int> farTargets)
    {
        var lines = disassembly.Split('\n');
        var resultLines = new List<string>();
        const int maxInstLength = 4;

        // The decoder marks every procedure/entry point with an anonymous label
        // (p_0xOFFSET) and, sometimes, with a banner comment above it. When the real
        // symbol of that location is known, the label and its banner are replaced
        // with the symbol instead of being duplicated
        var headerStart = -1;
        var blockStart = -1;
        var labelIndex = -1;
        var labelOffset = -1;
        
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            var labelMatch = DecoderLabelPattern.Match(line);
            if (labelMatch.Success)
            {
                blockStart = headerStart >= 0 ? headerStart : resultLines.Count;
                labelIndex = resultLines.Count;
                labelOffset = Convert.ToInt32(labelMatch.Groups["offset"].Value, 16);
                headerStart = -1;
                resultLines.Add(line);
                continue;
            }

            // "; Procedure at 0x... instruction offset" banner which belongs to the
            // label below it
            if (DecoderProcedureHeaderPattern.IsMatch(line))
            {
                if (headerStart < 0) headerStart = resultLines.Count;
                resultLines.Add(line);
                continue;
            }

            // Any other line breaks the banner, the label which follows has none
            headerStart = -1;

            // Linear Executable keeps a far pointer as one 32-bit value. The Intel
            // decoder prints those 4 bytes as a 16:16 segment:offset pair (Ap/Mp
            // operand) - both halves are joined back here, because in LE/LX such an
            // address is not always a far one. Every joined value becomes a candidate
            // entry point for the next analysis pass, so the jumps and the calls
            // with a big address are analysed too
            line = FarAddressPattern.Replace(line, match =>
            {
                var flat = ((uint)Convert.ToInt32(match.Groups["seg"].Value, 16) << 16)
                           | (uint)Convert.ToInt32(match.Groups["off"].Value, 16);
                farTargets.Add(unchecked((int)flat));
                return $"{match.Groups["mnemonic"].Value} 0x{flat:X8}";
            });

            var offMatch = Regex.Match(line, @";\s+0x([0-9A-Fa-f]+)\s");
            if (!offMatch.Success)
            {
                resultLines.Add(line);
                continue;
            }

            var localOff = int.Parse(offMatch.Groups[1].Value, NumberStyles.HexNumber);

            if (_exportAt.TryGetValue((objectNumber, localOff), out var expName))
            {
                AddProcedureLabel(resultLines, ref blockStart, ref labelIndex, ref labelOffset, localOff, $"{expName}:");
            }
            else if (objectNumber == _mainObj && localOff == _mainEip)
            {
                AddProcedureLabel(resultLines, ref blockStart, ref labelIndex, ref labelOffset, localOff, "main:");
            }
            else
            {
                blockStart = -1;
                labelIndex = -1;
                labelOffset = -1;
            }

            line = line.Replace($"; 0x{localOff:X4}", $"; {objectNumber}:0x{localOff:X4}");

            var fixupsInInst = new List<(int offset, string symbol, int size)>();
            for (var delta = 0; delta <= maxInstLength; delta++)
            {
                var checkOff = localOff + delta;
                if (_fixupSymbolAt.TryGetValue((objectNumber, checkOff), out var fixupInfo))
                {
                    fixupsInInst.Add((checkOff, fixupInfo.symbol, size: fixupInfo.pageNumber));
                }
            }

            if (fixupsInInst.Count > 0)
            {
                var comments = "xref: " + string.Join(", ", fixupsInInst.Select(f => $"{f.symbol}+0x{f.offset:X}"));
                line = $"{line.TrimEnd()} {comments}";
            }

            resultLines.Add(line);
        }

        return string.Join("\n", resultLines);
    }

    /// <summary>
    /// Matches a direct far control flow instruction with the address as the Intel
    /// decoder prints it - a 16:16 segment:offset pair of the Ap/Mp operand:
    ///     CALLF 0xD02E:0x0174
    /// </summary>
    private static readonly Regex FarAddressPattern = new(
        @"(?<mnemonic>CALLF|JMPF|CALL|JMP|PUSH|POP)\s+0x(?<seg>[0-9A-Fa-f]{1,4}):0x(?<off>[0-9A-Fa-f]{1,4})",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches the label which the Intel decoder puts in front of a procedure
    /// (a CALL target) or of an entry point:
    ///     p_0x0100:
    ///     __0x0200:
    /// Such a label is anonymous, so it is removed as soon as the real symbol
    /// of the procedure is known (see <see cref="AddProcedureLabel"/>)
    /// </summary>
    private static readonly Regex DecoderLabelPattern = new(
        @"^[ \t]*(?:p_0x|__0x)(?<offset>[0-9A-Fa-f]+):[ \t]*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches the header which the decoder prints above a procedure/entry point
    /// label. Such a comment belongs to the label below it only, so it is dropped
    /// together with the anonymous label it introduces:
    ///     ;
    ///     ; Procedure at 0x0100 instruction offset
    ///     ;
    ///     p_0x0100:
    /// </summary>
    private static readonly Regex DecoderProcedureHeaderPattern = new(
        @"^[ \t]*;[ \t]*(?:$|Entry point at 0x[0-9A-Fa-f]+|Procedure at 0x[0-9A-Fa-f]+(?: instruction offset)?)",
        RegexOptions.Compiled);

    /// <summary>
    /// Puts the resolved name of the procedure in front of the instruction and
    /// drops the anonymous label of the decoder when it points at the very same offset
    /// </summary>
    private static void AddProcedureLabel(
        List<string> resultLines,
        ref int blockStart,
        ref int labelIndex,
        ref int labelOffset,
        int localOff,
        string label)
    {
        if (labelOffset == localOff && blockStart >= 0 && labelIndex >= blockStart && labelIndex < resultLines.Count)
            resultLines.RemoveRange(blockStart, labelIndex - blockStart + 1);

        resultLines.Add(label);

        blockStart = -1;
        labelIndex = -1;
        labelOffset = -1;
    }

    /// <summary>
    /// Matches an instruction with an immediate operand which may be an address,
    /// together with its debug annotation:
    ///     PUSH 0x0000          ; 1:0x0000 68 00 00 // Replaces by PUSH OFFSET DLL::PROCEDURE+0xABCD
    ///     MOV AX, 0x1234       ; 1:0x0017 B8 34 12
    ///     MOV EAX, 0x00401000  ; 2:0x1A00 B8 00 10 40 00
    ///     CALLF 0x00001684     ; 1:0x0078 9A 84 16 00 00 // Replaces by export
    /// The address is either a flat 32-bit value (Linear Executables), a legacy
    /// 16:16 segment:offset pair, or a bare word of the 16-bit code. The
    /// <see cref="FarAddressPattern"/> already joined the far pointers into a flat
    /// 32-bit value, but the segment form is accepted as well.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*(?<mnemonic>[A-Z][A-Z0-9]*)[ \t]+[^;]*?(?<addr>0x(?:(?<flat>[0-9A-Fa-f]{8})|(?<seg>[0-9A-Fa-f]{1,4}):0x(?<off>[0-9A-Fa-f]{1,4})|(?<word>[0-9A-Fa-f]{4,})))[^;]*?[ \t]*;[ \t]*(?<instr>(?<instrObject>[0-9]+):(?<instrOffset>0x[0-9A-Fa-f]+))")]
    private static partial Regex InstructionPattern();

    /// <summary>
    /// Target offset of the instruction matched by
    /// <see cref="InstructionPattern"/>. A flat 32-bit LE address keeps the offset
    /// in its low word, so both notations give the same result. Returns -1 when
    /// the operand is not an address at all.
    /// </summary>
    private static int FarTargetOffset(Match m)
    {
        if (m.Groups["flat"].Success)
            return (int)(Convert.ToUInt32(m.Groups["flat"].Value, 16) & 0xFFFF);

        if (m.Groups["off"].Success)
            return Convert.ToInt32(m.Groups["off"].Value, 16);

        return m.Groups["word"].Success
            ? Convert.ToInt32(m.Groups["word"].Value, 16)
            : -1;
    }
}