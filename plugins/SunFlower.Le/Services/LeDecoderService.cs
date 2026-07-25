//
// CoffeeLake (C) 2026-*
//
// Be ready to see it. This is a real shit u know... 
// But it FINALLY WORKS fine and IDA 8.3 tells this the relocation matches right.
// 
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
    private readonly Dictionary<(int obj, int off), (string symbol, int size)> _fixupSymbolAt = [];

    /// <summary>
    /// Import Procedure Names from the import procedure table. Key is a NameOffset 
    /// </summary>
    private readonly Dictionary<uint, string> _importNameByOffset = [];

    private List<string> _results = [];
    private readonly int _mainObj;
    private readonly uint _mainEip;
    private readonly int _pageSize;

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
            $"; Fixup Records: {_dump.FixupRecords.Length}",
            $"; Imports: {_dump.ImportRecords.Length}",
            ";"
        ]);

        BuildExportMap();
        BuildImportNameMap();
        BuildFixupSymbolMap();
        
        for (var i = 0; i < _dump.Objects.Length; i++)
        {
            var obj = _dump.Objects[i];
            if (!obj.Execute || obj.VirtualSegmentSize == 0) continue;
            TranslateObject(i + 1, obj);
        }

        DescribePseudocode();

        return _results.ToArray();
    }
    
    private void DescribePseudocode()
    {
        // Firstly resolve fixup records because they have a high priority 
        // Export characters might have a references by the same file positions but
        // resolving of them is very hard
        var lines = _results.SelectMany(s => s.Split('\n')).ToArray();
        var modified = new List<string>();
        foreach (var resultLine in lines)
        {
            var line = resultLine.TrimEnd('\r');

            var m = ControlFlowPattern().Match(line);
            if (!m.Success)
            {
                modified.Add(line);
                continue;
            }
            // match relocs by file address: JMP 0x0002:0x0010 ; 3:0x1684 9A 10 00 02 00
            // Instruction address must be the same with a key of fixup record
            // The far address by given instruction ++offset replaces by the fixup record value (bad ptr => internal reference)
            var instructionObject = Convert.ToInt32(m.Groups[4].Value);
            var instructionOffset = Convert.ToInt32(m.Groups[5].Value, 16);
            var targetObject = Convert.ToInt16(m.Groups[1].Value, 16);
            var targetOffset = Convert.ToInt32(m.Groups[2].Value, 16);

            if (_fixupSymbolAt.TryGetValue((instructionObject, instructionOffset + 1), out var symbol))
            {
                line = line.Replace($"0x{targetObject:X4}:0x{targetOffset:X4}", $"{symbol}");
            }

            // Then if address still exists -> trying to replace it by exporting address
            m = ControlFlowPattern().Match(line); // again

            if (!m.Success)
            {
                modified.Add(line);
                continue;
            }
            
            targetObject = Convert.ToInt16(m.Groups[1].Value, 16);
            targetOffset = Convert.ToInt32(m.Groups[2].Value, 16);

            var export = _exportAt.FirstOrDefault(x => x.Key.off == targetOffset).Value;
            if (export is not null)
            {
                line = line.Replace($"0x{targetObject:X4}:0x{targetOffset:X4}", $"::{export}");
            }

            modified.Add(line);
        }

        _results = modified;
    }

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
                if (entry is EntryUnused)
                {
                    ordinal++;
                    continue;
                }

                var name = FindExportNameByOrdinal(ordinal);

                if (string.IsNullOrEmpty(name))
                {
                    ordinal++;
                    continue;
                }

                var offset = entry switch
                {
                    Entry16Bit e16 => e16.Offset,
                    Entry32Bit e32 => (int)e32.Offset,
                    Entry286CallGate eg => eg.Offset,
                    _ => -1
                };

                if (offset >= 0 && objNum > 0)
                    _exportAt[(objNum, offset)] = FailSafe(name);

                ordinal++;
            }
        }
    }

    private string? FindExportNameByOrdinal(int ordinal)
        => _dump.ResidentNames.FirstOrDefault(n => n.Ordinal == ordinal)?.String
           ?? _dump.NonResidentNames.FirstOrDefault(n => n.Ordinal == ordinal)?.String;

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

    private void BuildImportNameMap()
    {
        var baseOffset = _dump.MzHeader.e_lfanew + _dump.LeHeader.e32_impproc;
        using var fs = File.OpenRead(_filePath);
        using var reader = new BinaryReader(fs);
        reader.BaseStream.Seek(baseOffset, SeekOrigin.Begin);

        while (true)
        {
            var len = reader.ReadByte();
            if (len == 0)
                break;
            var offset = (uint)(reader.BaseStream.Position - baseOffset - 1);
            var nameBytes = reader.ReadBytes(len);
            var name = Encoding.ASCII.GetString(nameBytes);

            _importNameByOffset[offset] = name;
        }
    }

    private void BuildFixupSymbolMap()
    {
        var fpo = _dump.FixupPageOffsets;
        if (fpo.Length < 2) return;

        var logicalPageToOwner = new Dictionary<int, (int objNum, int pageIdx)>();
        for (var oi = 0; oi < _dump.Objects.Length; oi++)
        {
            var obj = _dump.Objects[oi];
            if (obj.PageMapEntries == 0) continue;
            var startLogical = (int)_dump.Pages[obj.PageMapIndex - 1].Page.LongPageIndex; // (int)obj.PageMapIndex - 1; // 0-based
            for (var pi = 0; pi < obj.PageMapEntries; pi++)
            {
                var logicalPage = startLogical + pi; // +0 +1 +2 ...
                logicalPageToOwner[logicalPage] = (oi + 1, pi);
                
                Console.WriteLine($"lpag#={logicalPage} -> (obj#{oi + 1} page[{pi}])");
            }
        }

        uint curOffset = 0;
        var currentLogicalPage = 0;

        foreach (var fixup in _dump.FixupRecords)
        {
            while (currentLogicalPage < fpo.Length - 1 && curOffset >= fpo[currentLogicalPage + 1].Offset)
                currentLogicalPage++;

            if (!logicalPageToOwner.TryGetValue(currentLogicalPage, out var owner))
            {
                AdvancePosition(ref curOffset, fixup);
                continue;
            }

            var (objNum, pageIdx) = owner;
            var offsets = fixup.SourceType.HasSourceList ? fixup.SourceOffsetList : [fixup.SourceOffset];
            var symbol = ResolveFixupSymbol(fixup);

            if (symbol != null)
            {
                foreach (var srcOff in offsets)
                {
                    var objOffset = pageIdx * _pageSize + srcOff;
                    if (_fixupSymbolAt.ContainsKey((objNum, objOffset)))
                        continue;

                    var size = fixup.RelocationFlags.Is32BitTargetOffset ? 4 : 2;
                    _fixupSymbolAt[(objNum, objOffset)] = (symbol, size);
                }
            }

            AdvancePosition(ref curOffset, fixup);
        }
    }

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
        return _importNameByOffset.TryGetValue(nameOffset, out var name) ? name : string.Empty;
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
            var pageType = (byte)(rawFlags & 0x03);

            if (pageType is not (0 or 3))
                continue;

            var fileOffset = _dump.LeHeader.e32_datapage + (page.LongPageIndex - 1) * _pageSize;

            Console.WriteLine($" -> Located 0x{fileOffset:X}");

            var isLastPage = (rawFlags & 0x80) != 0;

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
                Console.WriteLine($"[{nameof(BuildObjectBytesByPageIndex)}]: {e}");
            }
        }

        return buf;
    }

    private void TranslateObject(int objectNumber, Object obj)
    {
        var modeLabel = (obj.ObjectFlagsMask & 0x2000) != 0 ? "32-bit" : "16-bit";
        var suggestedName = Object.GetSuggestedNameByPermissions(obj);

        _results.Add("");
        _results.Add($"; === Object#{objectNumber} : {suggestedName} [{string.Join(", ", obj.ObjectFlags)}] ===");
        _results.Add($";     {modeLabel}, Virtual Size: {obj.VirtualSegmentSize} bytes");

        // TODO: 
        var objBytes = BuildObjectBytesByPageIndex(obj); 
        if (objBytes == null || objBytes.Length == 0) return;

        var entryPoints = new List<int>();
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
                    _ => -1
                };
                if (off >= 0 && off < objBytes.Length)
                    entryPoints.Add(off);
            }
        }

        if (entryPoints.Count == 0) entryPoints.Add(0);

        string disassembly;
        try
        {
            disassembly = (obj.ObjectFlagsMask & 0x2000) != 0
                ? I80386Decoder.decodeRecursive("", objBytes, [.. entryPoints.Order()])
                : I80286Decoder.decodeRecursive("", objBytes, [.. entryPoints.Order()]);

            var annotated = DescribeFragment(disassembly, objectNumber);
            _results.Add(annotated);
        }
        catch (Exception ex)
        {
            disassembly = ex.Message;
            _results.Add($"; Fatal: {ex.Message}");
        }
    }

    private string DescribeFragment(string disassembly, int objectNumber)
    {
        var lines = disassembly.Split('\n');
        var resultLines = new List<string>();
        const int maxInstLength = 15;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (Regex.IsMatch(line, @"\s+Entry point at\s+")) continue;
            if (Regex.IsMatch(line, @"\s+p_0x[0-9A-Fa-f]+:")) continue;
            if (Regex.IsMatch(line, @"\s+__0x[0-9A-Fa-f]+:")) continue;

            var offMatch = Regex.Match(line, @";\s+0x([0-9A-Fa-f]+)\s");
            if (!offMatch.Success)
            {
                resultLines.Add(line);
                continue;
            }

            var localOff = int.Parse(offMatch.Groups[1].Value, NumberStyles.HexNumber);

            if (_exportAt.TryGetValue((objectNumber, localOff), out var expName))
            {
                resultLines.Add($"{expName}:");
            }
            else if (objectNumber == _mainObj && localOff == _mainEip)
            {
                resultLines.Add($"main: ; {objectNumber}:0x{localOff:X4}");
            }

            line = line.Replace($"; 0x{localOff:X4}", $"; {objectNumber}:0x{localOff:X4}");

            var fixupsInInst = new List<(int offset, string symbol, int size)>();
            for (var delta = 0; delta <= maxInstLength; delta++)
            {
                var checkOff = localOff + delta;
                if (_fixupSymbolAt.TryGetValue((objectNumber, checkOff), out var fixupInfo))
                {
                    fixupsInInst.Add((checkOff, fixupInfo.symbol, fixupInfo.size));
                }
            }

            if (fixupsInInst.Count > 0)
            {
                var comments = "possible references: " + string.Join(", ", fixupsInInst.Select(f => $"{f.symbol}"));
                line += $"{comments}";
            }

            resultLines.Add(line);
        }

        return string.Join("\n", resultLines);
    }

    [GeneratedRegex(@"[CALLF|JMP|JMPF]\s+0x([0-9A-Fa-f]+):0x([0-9A-Fa-f]+)\s+;\s+(([0-9]+):0x([0-9A-Fa-f]+))")]
    private static partial Regex ControlFlowPattern();
}