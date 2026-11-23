// CoffeeLake (C) 2026-*
//
// NE decoder rewritten on LE principles (DecoderServiceBase):
//   - per-segment (.CODE) analysis instead of flat image
//   - export map from entry table bundles (ordinal -> symbol)
//   - fixup map from per-segment relocations (offset -> symbol)
//   - analysis loop: decode -> discover far targets -> re-decode (max 8 passes)
//   - operand analysis with fixup substitution using structured models
//     (DecodedInstruction / DecodedOperand / OperandPart) - NO regular expressions
//
// No factory. No regexes during analysis.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Text;
using Sunflower.Dasm.Intel.Core;
using Decoder = Sunflower.Dasm.Intel.Core.Decoder;
using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;

namespace SunFlower.Ne.Services;


public sealed class NeDecoderService
{
    private sealed class FixupSite
    {
        public readonly int Segment;
        public readonly int Offset;
        public readonly int Size;
        public readonly string Symbol;

        public FixupSite(int segment, int offset, int size, string symbol)
        {
            Segment = segment;
            Offset = offset;
            Size = size;
            Symbol = symbol;
        }
    }

    private sealed record AnalysedOperand(string Text);

    private sealed record AnalysedInstruction(
        DecodedInstruction Instr,
        string? Label,
        bool IsEntryPoint,
        bool IsCallTarget,
        IReadOnlyList<AnalysedOperand> Operands,
        IReadOnlyList<FixupSite> AllFixups);

    private readonly NeDumpManager _dump;
    private readonly string _filePath;
    private readonly ProcessorMode _mode;
    private readonly int _alignmentShift;

    private string InterruptMap => Path.Combine(AppContext.BaseDirectory, "Interrupt", "dos.json");
    private string OpcodeMap => Path.Combine(AppContext.BaseDirectory, "opcodes286.json");
    private string ExtendedOpcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
    
    private readonly Dictionary<int, string> _exportByOrdinal = [];
    private readonly Dictionary<(int seg, int off), FixupSite> _fixupAt = [];
    private readonly List<string> _results = [];

    public NeDecoderService(string filePath, NeDumpManager dump, ProcessorMode set)
    {
        _filePath = filePath;
        _dump = dump;
        _mode = set;
        _alignmentShift = dump.NeHeader.NE_Alignment == 0 ? 9 : dump.NeHeader.NE_Alignment;

        BuildExportMap();
        BuildFixupMap();
    }

    public string[] Decode()
    {
        var cpuName = _mode.Equals(ProcessorMode.I386) ? "Intel 80386" : "Intel 80286";
        _results.AddRange([
            $"; SunFlower.NE.dll for NE executables (at {DateTime.UtcNow:u})",
            $"; Disassembled using {cpuName} instruction set",
            ";"
        ]);

        PrintEntryPoints();

        foreach (var segment in _dump.Segments
                     .Where(s => s.Type == ".CODE")
                     .OrderBy(s => s.SegmentNumber))
        {
            TranslateSegment(segment);
        }

        return _results.ToArray();
    }

    private void PrintEntryPoints()
    {
        if (_exportByOrdinal.Count == 0) return;

        var exports = _exportByOrdinal
            .OrderBy(x => x.Key)
            .Select(x => $";     [{x.Key}] {x.Value}");

        _results.Add("; Export procedures");
        _results.AddRange(exports);
        _results.Add(";");
    }

    private void BuildExportMap()
    {
        var ordinal = 1;
        foreach (var ep in _dump.EntryBundles.SelectMany(b => b.EntryPoints))
        {
            if (ep.Type == "[UNUSED]")
            {
                ordinal++;
                continue;
            }

            var name = FindExportNameByOrdinal(ordinal);
            _exportByOrdinal[ordinal] =
                string.IsNullOrEmpty(name) ? $"@{ordinal}" : SanitizeSymbol(name);

            ordinal++;
        }
    }

    private string? FindExportNameByOrdinal(int ordinal)
    {
        return _dump.ResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String ??
               _dump.NonResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String;
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

    private void BuildFixupMap()
    {
        foreach (var segment in _dump.Segments)
        {
            foreach (var rel in segment.Relocations)
            {
                var size = GetFixupSize(rel.AddressType);
                if (size <= 0) continue;

                string? symbol = null;

                if (rel.RelocationType.Equals("Import", StringComparison.OrdinalIgnoreCase))
                {
                    symbol = ResolveImportSymbol(rel);
                }
                else if (rel.RelocationType.Equals("Internal", StringComparison.OrdinalIgnoreCase))
                {
                    symbol = ResolveInternalSymbol(rel);
                }

                if (symbol is null) continue;

                _fixupAt[((int)rel.SegmentNumber, rel.OffsetInSegment)] =
                    new FixupSite((int)rel.SegmentNumber, rel.OffsetInSegment, size, symbol);
            }
        }
    }

    private static int GetFixupSize(byte addressType) => addressType switch
    {
        0 => 1, // LowByte
        2 => 2, // Segment 16
        3 => 4, // 16:16
        5 => 2, // Offset16
        _ => 2
    };

    private string? ResolveImportSymbol(Relocation rel)
    {
        if (rel.Ordinal > 0)
        {
            foreach (var (_, imports) in _dump.ImportModels)
            {
                var match = imports.FirstOrDefault(i =>
                    i.ModuleIndex == rel.ModuleIndex && i.Ordinal == "@" + rel.Ordinal);
                
                if (match != null)
                    return $"{match.Module}::{match.Ordinal}";
            }
            return $"::?@{rel.Ordinal}";
        }

        if (rel.NameOffset > 0)
        {
            foreach (var (_, imports) in _dump.ImportModels)
            {
                var match = imports.FirstOrDefault(i =>
                    i.ModuleIndex == rel.ModuleIndex && i.NameOffset == rel.NameOffset);
                if (match != null)
                    return $"{match.Module}::{match.Procedure}";
            }
            return $"module[{rel.ModuleIndex}]::name_0x{rel.NameOffset:X4}";
        }

        return null;
    }

    private string ResolveInternalSymbol(Relocation rel)
    {
        if (rel.SegmentType == 0xFF)
        {
            var name = FindExportNameByOrdinal(rel.Target);
            return string.IsNullOrEmpty(name) ? $"@{rel.Target}" : SanitizeSymbol(name);
        }

        return $"::{ProcedurePrefix}_0x{rel.SegmentType:X4}{rel.Target:X4}";
    }

    private void TranslateSegment(SegmentModel segment)
    {
        var bytes = ReadSegmentBytes(segment);
        if (bytes.Length == 0) return;

        var entryPoints = CollectEntryPoints(segment, bytes);

        var (instructions, entrySet, _) = RunAnalysisLoop(bytes, entryPoints);

        var analysed = AnalyseInstructions((int)segment.SegmentNumber, instructions, entrySet);
        RenderSegment((int)segment.SegmentNumber, bytes, analysed);
    }

    private byte[] ReadSegmentBytes(SegmentModel segment)
    {
        var physicalOffset = (int)segment.FileOffset * (1 << _alignmentShift);
        var length = (int)segment.FileLength;
        
        if (length == 0) 
            return [];

        var bytes = new byte[length];
        using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        fs.Position = physicalOffset;
        var read = fs.Read(bytes, 0, length);
        if (read < length)
            Array.Resize(ref bytes, read);
        return bytes;
    }

    private SortedSet<int> CollectEntryPoints(SegmentModel segment, byte[] bytes)
    {
        var entryPoints = new SortedSet<int>();

        var mainCs = _dump.NeHeader.NE_CsIp >> 16;
        var mainIp = (int)(_dump.NeHeader.NE_CsIp & 0xFFFF);
        if (segment.SegmentNumber == mainCs && mainIp >= 0 && mainIp < bytes.Length)
            entryPoints.Add(mainIp);
    
        foreach (var entry in _dump.EntryBundles.SelectMany(b => b.EntryPoints))
        {
            if (entry.Segment != segment.SegmentNumber) continue;
            
            var off = entry.Offset;
            if (off < bytes.Length)
                entryPoints.Add(off);
        
        }

        return entryPoints;
    }

    private (IReadOnlyList<DecodedInstruction> instructions, SortedSet<int> entrySet, int followed)
        RunAnalysisLoop(byte[] bytes, SortedSet<int> entryPoints)
    {
        IReadOnlyList<DecodedInstruction> result = [];
        var entrySet = new SortedSet<int>();
        var followed = 0;

        for (var pass = 0; pass < 8; pass++)
        {
            var (instrs, entries) = DecodeBytes(bytes, [.. entryPoints]);

            var farTargets = CollectFarTargets(instrs, bytes);
            var discovered = farTargets.Count(entryPoints.Add);
            followed += discovered;

            result = instrs;
            entrySet = entries;

            if (discovered == 0) break;
        }

        return (result, entrySet, followed);
    }

    private (IReadOnlyList<DecodedInstruction> instructions, SortedSet<int> entries)
        DecodeBytes(byte[] bytes, int[] entryPoints)
    {
        var opcodeMap = _mode.Equals(ProcessorMode.I386) 
            ? ExtendedOpcodeMap 
            : OpcodeMap;
        
        var state = Decoder.create(opcodeMap, InterruptMap, _mode);
        var (list, _, _) = Decoder.runRecursive(state, bytes, entryPoints);

        return (list, []);
    }

    private static HashSet<int> CollectFarTargets(
        IReadOnlyList<DecodedInstruction> instructions, byte[] bytes)
    {
        var result = new HashSet<int>();

        foreach (var instr in instructions)
        foreach (var op in instr.Operands)
        {
            if (op.Part is not OperandPart.Far far) 
                continue;
            
            var targetOff = unchecked((int)far.Item2);
            if (targetOff > 0 && targetOff < bytes.Length && !IsZeroRun(bytes, targetOff))
                result.Add(targetOff);
        }

        return result;
    }

    private static bool IsZeroRun(byte[] bytes, int off) =>
        bytes[off] == 0 && off + 1 < bytes.Length && bytes[off + 1] == 0;
    
    private List<AnalysedInstruction> AnalyseInstructions(
        int segmentNumber,
        IReadOnlyList<DecodedInstruction> instructions,
        SortedSet<int> entrySet)
    {
        var callTargets = new HashSet<int>(
            instructions
                .Where(i => i.Flow.Equals(FlowControl.Call))
                .SelectMany(i => i.Targets));

        var result = new List<AnalysedInstruction>(instructions.Count);

        foreach (var instr in instructions)
        {
            var label = ResolveLabel(segmentNumber, instr.Offset, entrySet);
            var instrFixups = CollectFixupsForRange(segmentNumber, instr.Offset, instr.Length);
            var analysedOps = new List<AnalysedOperand>(instr.Operands.Length);

            analysedOps.AddRange(
                from op in instr.Operands
                let opFixup = FindFixupForOperand(instr.Offset, op, instrFixups)
                let text = FormatOperandWithSubstitution(instr, op, opFixup, _mode)
                select new AnalysedOperand(text)
            );

            result.Add(new AnalysedInstruction(
                instr,
                label,
                entrySet.Contains(instr.Offset),
                callTargets.Contains(instr.Offset),
                analysedOps,
                instrFixups));
        }

        return result;
    }

    private string? ResolveLabel(int segmentNumber, int offset, SortedSet<int> entrySet)
    {
        if (!entrySet.Contains(offset)) return null;

        var mainCs = _dump.NeHeader.NE_CsIp >> 16;
        var mainIp = (int)(_dump.NeHeader.NE_CsIp & 0xFFFF);
        
        if ((uint)segmentNumber == mainCs && offset == mainIp)
            return "main";
        
        foreach (var bundle in _dump.EntryBundles.SelectMany(b => b.EntryPoints))
        {
            if (bundle.Segment != segmentNumber) 
                continue;
            
            if (bundle.Offset == offset && !string.IsNullOrEmpty(bundle.Name))
                return bundle.Name;
        }

        return $"{ProcedurePrefix}_0x{segmentNumber:X4}{offset:X4}";
    }

    private List<FixupSite> CollectFixupsForRange(int segment, int instrOffset, int instrLength)
    {
        var result = new List<FixupSite>();
        for (var i = 0; i < instrLength; i++)
        {
            var site = instrOffset + i;
            if (_fixupAt.TryGetValue((segment, site), out var fixup))
                result.Add(fixup);
        }
        return result;
    }

    private static FixupSite? FindFixupForOperand(int instrOffset, DecodedOperand op, IReadOnlyList<FixupSite> instrFixups)
    {
        if (op.BytesLength <= 0 || instrFixups.Count == 0) return null;

        var opStart = instrOffset + op.BytesOffset;
        var opEnd = opStart + op.BytesLength;

        return (
            from f in instrFixups 
            let fStart = f.Offset 
            let fEnd = fStart + f.Size 
            where fStart < opEnd && fEnd > opStart 
            select f
        ).FirstOrDefault();
    }

    private static string FormatOperandWithSubstitution(
        DecodedInstruction instr, DecodedOperand op, FixupSite? fixup, ProcessorMode mode)
    {
        if (fixup is null) 
            return FormatOperandPlain(instr, op, mode);
        
        var text = op.Part switch
        {
            OperandPart.Far => fixup.Symbol,
            OperandPart.Imm => fixup.Symbol,
            OperandPart.SignedImm => fixup.Symbol,
            OperandPart.Abs => $"[{fixup.Symbol}]",
            OperandPart.Mem mem => ReplaceMemDisplacement(mem.Item, fixup.Symbol),
            _ => FormatOperandPlain(instr, op, mode)
        };
        
        return text;
    }

    private static string FormatOperandPlain(
        DecodedInstruction instr, DecodedOperand op, ProcessorMode mode) =>
        Decoder.formatOperandPart(mode, instr.Offset, op.Part);

    private static string ReplaceMemDisplacement(string memText, string symbol)
    {
        var plus = memText.LastIndexOf('+');
        var minus = memText.LastIndexOf('-');
        var cut = Math.Max(plus, minus);

        if (cut <= 0)
            return $"[{symbol}]";

        var head = memText[..cut];
        return $"{head}+{symbol}]";
    }
    private void RenderSegment(
        int segmentNumber, byte[] bytes,
        IReadOnlyList<AnalysedInstruction> analysed)
    {
        var sb = new StringBuilder();
        var cover = 0;

        var ordered = analysed.OrderBy(a => a.Instr.Offset).ToArray();

        foreach (var analysedInstruction in ordered)
        {
            var instr = analysedInstruction.Instr;

            if (instr.Offset > cover)
                RenderDataRange(sb, segmentNumber, bytes, cover, instr.Offset - cover);

            cover = Math.Max(cover, instr.Offset + instr.Length);

            if (analysedInstruction.IsEntryPoint || analysedInstruction.IsCallTarget)
            {
                sb.AppendLine(";");
                var header = (analysedInstruction.IsEntryPoint, analysedInstruction.IsCallTarget) switch
                {
                    (true, true) => $"; Export & CALL target @ 0x{instr.Offset:X4}",
                    (true, false) => $"; Export @ 0x{instr.Offset:X4}",
                    _ => $"; Procedure at 0x{instr.Offset:X4}"
                };
                sb.AppendLine(header);
                sb.AppendLine(";");
                sb.AppendLine($"{analysedInstruction.Label ?? $"{ProcedurePrefix}_0x{segmentNumber:X4}{instr.Offset:X4}"}:");
            }
            else if (analysedInstruction.Label is not null)
            {
                sb.AppendLine($"{analysedInstruction.Label}:");
            }

            var operandText = string.Join(", ", analysedInstruction.Operands
                .Select(o => o.Text)
                .Where(t => t.Length > 0));

            var fullText = operandText.Length > 0
                ? $"{instr.Mnemonic} {operandText}"
                : instr.Mnemonic;

            var bytesStr = BitConverter.ToString(instr.Bytes).Replace("-", " ");

            var xrefComment = analysedInstruction.AllFixups.Count > 0
                ? " xref: " + string.Join(", ", analysedInstruction.AllFixups
                    .Select(f => f.Offset == instr.Offset
                        ? f.Symbol
                        : $"{f.Symbol}+0x{f.Offset - instr.Offset:X}"))
                : string.Empty;

            var suffixComment = instr.Comment is not null && instr.Comment.Value.Length > 0
                ? instr.Comment.Value
                : string.Empty;

            sb.AppendLine(
                $"\t{fullText,-30} ; {segmentNumber}:0x{instr.Offset:X4} {bytesStr}{suffixComment}{xrefComment}");

            if (instr.Flow.Equals(FlowControl.Return))
            {
                // TODO: last label name which returned a flow control
                sb.AppendLine($"; <-- Return to previous flow control");
            }
            else if (instr.Flow.Equals(FlowControl.Terminate))
            {
                // TODO: Which procedure/jump target terminated a program
                sb.AppendLine($"; Terminated ({suffixComment}");
            }
            else if (instr.Flow.Equals(FlowControl.Halt))
            {
                sb.AppendLine("; System Halted");
            }
        }

        if (cover < bytes.Length)
            RenderDataRange(sb, segmentNumber, bytes, cover, bytes.Length - cover);

        _results.Add(sb.ToString());
    }

    private const string ProcedurePrefix = "procedure";

    private static void RenderDataRange(
        StringBuilder sb, int segmentNumber, byte[] bytes, int offset, int length)
    {
        const int zeroThreshold = 4;
        const int bytesPerLine = 16;
        const int maxLines = 16;

        var mutableI = offset;
        var limit = offset + length;
        var linesLeft = maxLines;

        while (mutableI < limit && linesLeft > 0)
        {
            var runStart = mutableI;
            var runByte = bytes[mutableI];

            while (mutableI < limit && bytes[mutableI] == runByte) mutableI++;
            var runLength = mutableI - runStart;

            if (runByte == 0 && runLength >= zeroThreshold)
            {
                sb.AppendLine($"\tDB {runLength} DUP (0x00),".PadRight(30) +
                    $" ; {segmentNumber}:0x{runStart:X4} padding");
                continue;
            }

            var pos = runStart;
            while (pos < mutableI && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, mutableI - pos);
                var payload = string.Join(", ", bytes[pos..(pos + take)].Select(b => $"0x{b:X2}"));
                sb.AppendLine($"\tDB {payload},".PadRight(30) +
                    $" ; {segmentNumber}:0x{pos:X4} data");
                pos += take;
                linesLeft--;
            }
        }

        if (mutableI < limit)
            sb.AppendLine($"; {segmentNumber}:0x{mutableI:X4} {limit - mutableI} more byte(s) of data (omitted)");
    }
}