//
// CoffeeLake (C) 2026-*
//
// The NeDecoderService.cs represents NE disassembler which uses
// Intel 286/386 decoder API and knowledge base from NewExecutableSeed.
//
// Strategy:
//  1. Build flat memory image from all .CODE segments (via NeFlatImageBuilder)
//  2. Resolve internal relocations so far-call/jump targets become flat addresses
//  3. Disassemble recursively from ALL entry points at once (decodeRecursive)
//  4. Post-process: for every entry point offset, ensure there's a label.
//     Main entry (CS:IP from header) gets "main:" label.
//     Export entries get their symbol name.
//     Other entries get "entry_0xSEG:".
//  5. Convert flat addresses back to segment:offset for human readability.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.FSharp.Data.UnitSystems.SI.UnitNames;
using Sunflower.Dasm;
using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;

namespace SunFlower.Ne.Services;

public enum NeInstructionSet
{
    Intel286,
    Intel386
}

public class NeDecoderService
{
    private readonly NeDumpManager _dump;
    private readonly string _filePath;
    private readonly NeInstructionSet _set;

    // export ordinal -> symbol name
    private readonly Dictionary<int, string> _exportByName = [];
    // Main entry flat address (CS:IP from header)
    private int _mainEntryFlat = -1;

    private readonly List<string> _results = [];
    private FlatImageResult _flat = null!;

    public NeDecoderService(string filePath, NeDumpManager dump, NeInstructionSet set)
    {
        _filePath = filePath;
        _dump = dump;
        _set = set;
        BuildExportTable();
    }

    private void BuildExportTable()
    {
        var ordinal = 1;
        foreach (var ep in _dump.EntryBundles.SelectMany(bundle => bundle.EntryPoints))
        {
            if (ep.Type == "[UNUSED]")
            {
                ordinal++; 
                continue;
            }
            var name = FindExportNameByOrdinal(ordinal);
            // Important: 
            // Export names night be unsafe (OS/2 application might contain C++ abi or unreadable ASCII)
            _exportByName[ordinal] = string.IsNullOrEmpty(name) ? $"_@{ordinal}" : SanitizeSymbol(name);
            ordinal++;
        }
    }

    private string? FindExportNameByOrdinal(int ordinal)
    {
        var name = _dump.ResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String ?? 
                   _dump.NonResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String;
        return name;
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

    public string[] Decode()
    {
        var cpuName = _set switch
        {
            NeInstructionSet.Intel286 => "Intel 80286",
            NeInstructionSet.Intel386 => "Intel 80386",
            _ => "Intel 80x86"
        };

        _results.AddRange([
            $"; SunFlower.NE.dll for NE executables (at {DateTime.UtcNow})",
            $"; Disassembled using {cpuName} instruction set",
            ";"
        ]);

        PrintEntryPoints();

        var builder = new NeFlatImageBuilder(_dump);
        builder.SetExportNames(_exportByName);
        _flat = builder.Build();

        if (_flat.Size == 0)
        {
            _results.Add("; ERROR: No .CODE segments found or flat image is empty.");
            return _results.ToArray();
        }

        // Determine main entry flat address
        var mainCs = _dump.NeHeader.NE_CsIp >> 16;
        var mainIp = _dump.NeHeader.NE_CsIp & 0xFFFF;
        _mainEntryFlat = _flat.SegmentBase.TryGetValue(mainCs, out var baseAddr) ? baseAddr + (int)mainIp : -1;

        string mainDisassembly;
        try
        {
            var allEntryPoints = _flat.FlatEntryPoints.Length > 0 
                ? _flat.FlatEntryPoints 
                : [0];
            
            mainDisassembly = _set switch
            {
                NeInstructionSet.Intel286 =>
                    I80286Decoder.decodeRecursive("", _flat.Image, allEntryPoints),
                NeInstructionSet.Intel386 =>
                    I80386Decoder.decodeRecursive("", _flat.Image, allEntryPoints),
                _ => throw new InvalidOperationException("Unsupported instruction set")
            };
        }
        catch (Exception ex)
        {
            _results.Add($";     FATAL DECODE ERROR: {ex.Message}");
            return _results.ToArray();
        }

        var annotated = AnnotateFlat(mainDisassembly);
        _results.Add(annotated);

        return _results.ToArray();
    }

    private void PrintEntryPoints()
    {
        if (_exportByName.Count > 0)
        {
            var exports = new List<string>();
            foreach (var (ord, name) in _exportByName.OrderBy(x => x.Key))
                exports.Add($";     [{ord}] {name}");
            
            _results.Add("; Export procedures ");
            _results.AddRange(exports);
            _results.Add(";");
        }
    }

    /// <summary>
    /// Extracts the flat instruction offset from a disassembly line.
    /// Format from decoder: "0xADDR  [bytes]  MNEMONIC operands"
    /// Returns -1 if line is a label or invalid.
    /// </summary>
    private static int ExtractOffset(string line)
    {
        // Label lines: "__0xADDR:" or "p_0xADDR:" or spaces then label
        if (Regex.IsMatch(line, @"(?:__|p_|entry_)0x[0-9A-Fa-f]+:"))
            return -1;

        // Comment lines or header
        if (line.TrimStart().StartsWith(";"))
            return -1;

        // Instruction line: starts with offset
        var m = Regex.Match(line, @"\s*0x([0-9A-Fa-f]+)\s"); // inverse ^\s*0x([0-9A-Fa-f]+)\s
        if (m.Success && int.TryParse(m.Groups[1].Value,
            System.Globalization.NumberStyles.HexNumber, null, out var off))
            return off;

        return -1;
    }

    /// <summary>
    /// Post-processes the raw disassembly text:
    /// Ensures every entry point has a label (inserted if missing)
    /// Main entry gets "main:" label
    /// Export entries get their symbol name
    /// Converts flat offsets back to segment:offset comments
    /// </summary>
    private string AnnotateFlat(string disassembly)
    {
        var lines = disassembly.Split('\n');
        var entryFlatOrdered = _flat.FlatEntryPoints.OrderBy(e => e).ToArray();

        // Detect which offsets already have labels
        var existingLabels = new HashSet<int>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var m = Regex.Match(line, @"(?:__|p_|entry_)0x([0-9A-Fa-f]+)\s*:");
            if (m.Success)
                existingLabels.Add(Convert.ToInt32(m.Groups[1].Value, 16));
        }

        // Entry points that need a label forcibly inserted
        var needLabel = entryFlatOrdered.Where(e => !existingLabels.Contains(e)).ToList();

        // Step 2: Insert missing labels before their instruction
        var resultLines = new List<string>();
        
        var insertedLabels = new HashSet<int>();
        var remaining = new List<int>(needLabel);
        
        var lastOffset = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Check if this is a label line -> skip insertion checks for these
            if (Regex.IsMatch(line, @"(?:__|p_|entry_)0x[0-9A-Fa-f]+:"))
            {
                resultLines.Add(line);
                continue;
            }

            var offset = ExtractOffset(line);

            if (offset >= 0)
            {
                // Insert all pending entry points with offset <= current offset
                foreach (var epOff in remaining.OrderBy(o => o).ToArray())
                {
                    if (offset >= epOff && !insertedLabels.Contains(epOff))
                    {
                        resultLines.Add(BuildEntryLabel(epOff));
                        insertedLabels.Add(epOff);
                        remaining.Remove(epOff);
                    }
                }
                lastOffset = offset;
            }

            resultLines.Add(line);
        }

        // Handle remaining entry labels past end of decoded code
        foreach (var epOff in remaining.Where(epOff => !insertedLabels.Contains(epOff)))
        {
            resultLines.Add(BuildEntryLabel(epOff) + " ; (unreachable)");
            insertedLabels.Add(epOff);
        }

        var sb = new StringBuilder();
        var labeledOffsets = new HashSet<int>(insertedLabels);
        
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var modifiedLine = line;
            
            var lm = Regex.Match(line, @"^(?<indent>\s*)(?<prefix>__|p_)(?<addr>0x[0-9A-Fa-f]+)\s*:\s*(?<comment>.*)$");
            if (lm.Success)
            {
                var addrHex = lm.Groups["addr"].Value;
                var indent = lm.Groups["indent"].Value;
                var flatAddr = Convert.ToInt32(addrHex[2..], 16);

                if (labeledOffsets.Contains(flatAddr))
                    continue; // already inserted above
                
                labeledOffsets.Add(flatAddr);

                var segOff = _flat.AddressMap.TryGetValue(flatAddr, out var src)
                    ? src.ToString() : $"flat_0x{flatAddr:X4}";

                if (_flat.ExportAtFlat.TryGetValue(flatAddr, out var expName))
                    sb.AppendLine($"{indent}{expName}: ; Export entry at {segOff}");
                else
                    sb.AppendLine(line);
                
                continue;
            }
            var callMatch = Regex.Match(line, @"[CALLF|JMP]\s+0x([0-9A-Fa-f]+):0x([0-9A-Fa-f]+)\s+;\s+(0x([0-9A-Fa-f]+))");
            if (callMatch.Success)
            {
                var segHex = callMatch.Groups[1].Value;
                var offHex = callMatch.Groups[2].Value;
                var instrOffsetHex = callMatch.Groups[4].Value;
                
                if (int.TryParse(instrOffsetHex, NumberStyles.HexNumber, null, out var instrOffset) &&
                    int.TryParse(segHex, NumberStyles.HexNumber, null, out var segVal) &&
                    int.TryParse(offHex, NumberStyles.HexNumber, null, out var offVal))
                {
                    // call instruction was skipped -> real runtime import offset = ++instrOffset
                    var dataAddr = instrOffset + 1;
                    
                    if (_flat.AddressMap.TryGetValue(dataAddr, out var src))
                    {
                        var key = -((int)(src.Segment << 16) | src.Offset);
                        
                        if (_flat.ImportAtFlat.TryGetValue(key, out var impName))
                        {
                            modifiedLine = modifiedLine.Replace($"0x{segVal:X4}:0x{offVal:X4}", impName);
                        }
                    }
                
                    // export far?
                    if (_flat.SegmentBase.TryGetValue((uint)segVal, out var baseAddr))
                    {
                        var targetFlat = baseAddr + offVal;
                        if (_flat.ExportAtFlat.TryGetValue(targetFlat, out var expName))
                        {
                            modifiedLine = modifiedLine.Replace($"0x{segVal:X4}:0x{offVal:X4}", expName);
                        }
                    }
                    
                }
            }
            // Добавьте здесь другие проверки, если нужно (например, для RETF)

            sb.AppendLine(modifiedLine);
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// Builds a label line for an entry point at given flat address.
    /// </summary>
    private string BuildEntryLabel(int flatAddr)
    {
        var segOff = _flat.AddressMap.TryGetValue(flatAddr, out var src)
            ? src.ToString()
            : $"flat_0x{flatAddr:X4}";

        if (flatAddr == _mainEntryFlat)
            return $"main: ; Entry point at {segOff}";

        if (_flat.ExportAtFlat.TryGetValue(flatAddr, out var expName))
            return $"{expName}: ; Export entry at {flatAddr}";

        // Make sure that unnamed entry points scanned already
        return src != null 
            ? $"entry_{src.Offset:X4}: ; Entry point at {segOff}" 
            : $"entry_flat_{flatAddr:X8}: ; Entry point at {segOff}";
    }
}