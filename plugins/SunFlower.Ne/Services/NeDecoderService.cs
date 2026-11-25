// CoffeeLake (C) 2026-*
//
// NeDecoderService.cs represents a New Executable (OS/2 Win16) programs disassembler service
// can map: 
//   - export map from entry bundles (ordinal -> symbol) + entry->(seg,off)
//   - relocation map from per-segment relocations with full target info
//     (target segment/offset, address type, selector-only flag)
//   - multi-pass analysis loop (up to 8 passes):
//       decode -> recursive descent -> jump table discovery -> re-decode
//   - filtering of unreachable instructions
//   - local label registration for procedure/branch targets
//   - far operand formatting using both linker patterns:
//       A) selector-only fixup (AddressType=2)   -> offset from code bytes
//       B) full pointer fixup (AddressType=3/11) -> target from fixup record
//       C) offset-only fixup (AddressType=5/13)  -> target from fixup, seg from bytes
//   - crossreferences
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
    /// <summary>
    /// Resolved fixup site. Carries enough info so far operands can
    /// combine "segment from fixup" with "offset from code bytes"
    /// (classic linker behavior where one half is a placeholder).
    /// </summary>
    private sealed class FixupSite
    {
        public readonly int Segment; // source segment
        public readonly int Offset; // source offset within segment
        public readonly int Size; // bytes patched
        public readonly string Symbol; // resolved symbolic name

        public readonly int TargetSegment; // 0 = import / unresolved
        public readonly int TargetOffset;
        public readonly int Additive;
        public readonly bool IsSelectorOnly; // AddressType = 2

        public FixupSite(int segment, int offset, int size, string symbol,
            int targetSegment, int targetOffset, int additive, bool isSelectorOnly)
        {
            Segment = segment;
            Offset = offset;
            Size = size;
            Symbol = symbol;
            TargetSegment = targetSegment;
            TargetOffset = targetOffset;
            Additive = additive;
            IsSelectorOnly = isSelectorOnly;
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

    private const string ProcedurePrefix = "procedure";
    private const string LabelPrefix = "label";
    private const int MaxAnalysisPasses = 8;

    public bool DumpStrings { get; set; } = true;
    public int MinStringLength { get; set; } = 4;

    private readonly NeDumpManager _dump;
    private readonly ProcessorMode _mode;
    private readonly string _filePath;
    private readonly int _alignmentShift;

    private readonly Dictionary<int, string> _exportByOrdinal = [];
    private readonly Dictionary<int, (int seg, int off)> _entryByOrdinal = [];
    private readonly Dictionary<(int seg, int off), string> _exportAt = [];
    private readonly Dictionary<(int seg, int off), FixupSite> _fixupAt = [];
    private readonly Dictionary<(int seg, int off), string> _localLabelAt = [];
    private readonly Dictionary<int, SortedSet<int>> _fixupTargetsBySegment = [];
    private readonly List<string> _results = [];

    private string InterruptMap => Path.Combine(AppContext.BaseDirectory, "Interrupt", "dos.json");
    private string OpcodeMap => Path.Combine(AppContext.BaseDirectory, "opcodes286.json");
    private string ExtendedOpcodeMap => Path.Combine(AppContext.BaseDirectory, "opcodes386.json");

    private bool Is32Bit => _mode.Equals(ProcessorMode.I386);

    public NeDecoderService(string filePath, NeDumpManager dump, ProcessorMode set)
    {
        _filePath = filePath;
        _dump = dump;
        _mode = set;
        _alignmentShift = dump.NeHeader.NE_Alignment == 0 ? 9 : dump.NeHeader.NE_Alignment;

        BuildExportMap(); // must precede fixup map
        BuildFixupMap();
    }

    public string[] Decode()
    {
        var cpuName = Is32Bit ? "Intel 80386" : "Intel 80286";

        _results.AddRange([
            $";         This file is **generated** at ({DateTime.UtcNow:u}UTC)",
            "; The Sunflower.NE.DLL processes dumped program image and makes x86 pseudo code for it",
            "; CoffeeLake (C) 2026-*",
            $"; Disassembled using {cpuName} instruction set",
            $"; DOS interrupt map:{File.Exists(InterruptMap)}",
            $"; Total Segments:   {_dump.Segments.Count}",
            $"; Entry Bundles:    {_dump.EntryBundles.Count}",
            $"; Entry Points:     {_exportByOrdinal.Count}",
            $"; Fixup Records:    {_fixupAt.Count}",
            ";"
        ]);

        PrintEntryPoints();

        foreach (var segment in _dump.Segments
                     .Where(s => s.Type == ".CODE")
                     .OrderBy(s => s.SegmentNumber))
        {
            TranslateSegment(segment);
        }

        _results.Add("END");
        return _results.ToArray();
    }

    private void BuildExportMap()
    {
        // Main entry point first (may not be listed in entry bundles)
        var mainCs = (int)(_dump.NeHeader.NE_CsIp >> 16);
        var mainIp = (int)(_dump.NeHeader.NE_CsIp & 0xFFFF);
        if (mainCs > 0)
            _exportAt[(mainCs, mainIp)] = "main";

        var ordinal = 1;
        foreach (var ep in _dump.EntryBundles.SelectMany(b => b.EntryPoints))
        {
            if (ep.Type == "[UNUSED]")
            {
                ordinal++;
                continue;
            }

            var name = FindExportNameByOrdinal(ordinal);
            var exportName = string.IsNullOrEmpty(name) ? $"@{ordinal}" : SanitizeSymbol(name);

            _exportByOrdinal[ordinal] = exportName;

            if (ep.Segment > 0)
            {
                _entryByOrdinal[ordinal] = (ep.Segment, ep.Offset);
                _exportAt[(ep.Segment, ep.Offset)] = exportName;
            }

            ordinal++;
        }
    }

    private string? FindExportNameByOrdinal(int ordinal)
    {
        return
            _dump.ResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String ??
            _dump.NonResidentNames.FirstOrDefault(n => n.Ordinal == ordinal).String;
    }

    private static string SanitizeSymbol(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c is '_' or '@' or '.' or '$' ? c : '_');
        var r = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(r) ? "sym" : r;
    }

    private void PrintEntryPoints()
    {
        _results.Add("; Entry points:");
        foreach (var (ord, name) in _exportByOrdinal.OrderBy(x => x.Key))
        {
            if (_entryByOrdinal.TryGetValue(ord, out var loc))
                _results.Add($";     [{ord}] {name} @ {loc.seg:X4}:{loc.off:X4}");
            else
                _results.Add($";     [{ord}] {name}");
        }

        _results.Add(";");
    }

    private void BuildFixupMap()
    {
        foreach (var segment in _dump.Segments)
        {
            var segNum = (int)segment.SegmentNumber;
            foreach (var rel in segment.Relocations)
            {
                var size = GetFixupSize(rel.AddressType);
                if (size <= 0) continue;

                string? symbol = null;
                (int seg, int off) target = (0, 0);
                var isSelectorOnly = rel.AddressType == 2;

                if (rel.RelocationType.Equals("Import", StringComparison.OrdinalIgnoreCase))
                {
                    symbol = ResolveImportSymbol(rel);
                }
                else if (rel.RelocationType.Equals("Internal", StringComparison.OrdinalIgnoreCase))
                {
                    symbol = ResolveInternalSymbol(rel);
                    target = ResolveInternalTarget(rel);
                    RememberCodeReference(segNum, rel, target);
                }

                if (symbol is null) continue;

                _fixupAt[(segNum, rel.OffsetInSegment)] = new FixupSite(
                    segNum, rel.OffsetInSegment, size, symbol,
                    target.seg, target.off, 0, isSelectorOnly);
            }
        }
    }

    private static int GetFixupSize(byte addressType) => addressType switch
    {
        0 => 1, // LowByte
        2 => 2, // Selector16
        3 => 4, // Far / ptr16:16
        5 => 2, // Offset16
        11 => 6, // Far32 / ptr16:32
        13 => 4, // Offset32
        _ => 2
    };

    private (int seg, int off) ResolveInternalTarget(Relocation rel)
    {
        if (rel.SegmentType == 0xFF)
        {
            // Movable reference
            // SegmentType=0xFF,
            // Target=ordinal
            return _entryByOrdinal.TryGetValue(rel.Target, out var entry) ? entry : (0, 0);
        }

        // Fixed reference
        // SegmentType=segment number,
        // Target=offset
        return (rel.SegmentType, rel.Target);
    }

    private string? ResolveImportSymbol(Relocation rel)
    {
        if (rel.Ordinal > 0)
        {
            foreach (var (_, imports) in _dump.ImportModels)
            {
                var match = imports.FirstOrDefault(i =>
                    i.ModuleIndex == rel.ModuleIndex && i.Ordinal == "@" + rel.Ordinal);
                if (match != null) return $"{match.Module}::{match.Ordinal}";
            }

            return $"::?@{rel.Ordinal}";
        }

        if (rel.NameOffset > 0)
        {
            foreach (var (_, imports) in _dump.ImportModels)
            {
                var match = imports.FirstOrDefault(i =>
                    i.ModuleIndex == rel.ModuleIndex && i.NameOffset == rel.NameOffset);
                if (match != null) return $"{match.Module}::{match.Procedure}";
            }

            return $"MOD#{rel.ModuleIndex}::IMPTAB0x{rel.NameOffset:X4}";
        }

        return null;
    }

    private string ResolveInternalSymbol(Relocation rel)
    {
        if (rel.SegmentType == 0xFF)
        {
            var name = FindExportNameByOrdinal(rel.Target);
            
            return string.IsNullOrEmpty(name) 
                ? $"{_dump.ResidentNames[0].String}::@{rel.Target}" 
                : _dump.ResidentNames[0].String + "::" + SanitizeSymbol(name);
        }

        return $"{ProcedurePrefix}_0x{rel.SegmentType:X4}{rel.Target:X4}";
    }

    /// <summary>
    /// Feeds the seed set for the target segment so the recursive descent
    /// will pick up code reachable only through fixups
    /// expecting something the same with vtables or callbacks.
    /// </summary>
    private void RememberCodeReference(int sourceSegment, Relocation rel, (int seg, int off) target)
    {
        if (target.seg <= 0 || target.off < 0) return;

        var targetSegment = _dump.Segments.FirstOrDefault(s => (int)s.SegmentNumber == target.seg);
        if (targetSegment is null || targetSegment.Type != ".CODE") return;

        var sourceSegmentModel = _dump.Segments.FirstOrDefault(s => (int)s.SegmentNumber == sourceSegment);

        // Far pointer (16:16 or 16:32) => always a code reference
        var isCodeRef = rel.AddressType is 3 or 11;

        // Offset16/32 from .CODE into .CODE => internal branch/call target
        if (!isCodeRef
            && sourceSegmentModel is not null
            && sourceSegmentModel.Type == ".CODE"
            && rel.AddressType is 5 or 13)
        {
            isCodeRef = true;
        }

        if (!isCodeRef) return;

        if (!_fixupTargetsBySegment.TryGetValue(target.seg, out var offsets))
        {
            offsets = [];
            _fixupTargetsBySegment[target.seg] = offsets;
        }

        offsets.Add(target.off);
    }

    private void TranslateSegment(SegmentModel segment)
    {
        var bytes = ReadSegmentBytes(segment);
        var segNum = (int)segment.SegmentNumber;

        if (bytes.Length == 0)
        {
            EmitSegmentHeader(segNum, segment, 0, 0, 0, 0);
            _results.Add("; Empty or uninitialized segment");
            EmitSegmentFooter(segNum);
            return;
        }

        var seeds = CollectEntryPoints(segment, bytes);

        if (seeds.Count == 0)
        {
            EmitSegmentHeader(segNum, segment, 0, 0, 0, bytes.Length);
            _results.Add("; No entry points into this segment, kept as data");
            DumpSegmentStrings(segNum, bytes);
            EmitSegmentFooter(segNum);
            return;
        }

        // Pass #1: decode + jump table discovery loop
        var (allInstructions, jumpTableBytes) = RunAnalysisLoop(bytes, seeds, segNum);

        // Pass #2: recursive descent
        var reachable = RunRecursiveDescent(allInstructions, seeds, bytes, jumpTableBytes);

        // Pass #3: filter unreachable
        var kept = allInstructions
            .Where(i => reachable.Contains(i.Offset))
            .OrderBy(i => i.Offset)
            .ToArray();

        // Pass #4: local labels for branch/call targets
        BuildSegmentLocalLabels(segNum, kept);

        var exportCount = _exportAt.Keys.Count(k => k.seg == segNum);
        EmitSegmentHeader(segNum, segment, kept.Length, seeds.Count, exportCount, bytes.Length);

        if (kept.Length < allInstructions.Count)
            _results.Add($"; Filtered: {allInstructions.Count - kept.Length} unreachable instruction(s) removed");

        // Pass #5: finally render!!!!!!
        var analysed = AnalyseInstructions(segNum, kept, seeds);
        RenderSegment(segNum, bytes, analysed);
        DumpSegmentStrings(segNum, bytes);
        EmitSegmentFooter(segNum);
    }

    private byte[] ReadSegmentBytes(SegmentModel segment)
    {
        var physicalOffset = (int)segment.FileOffset * (1 << _alignmentShift);
        var length = (int)segment.FileLength;
        if (length == 0) return [];

        var bytes = new byte[length];
        using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        fs.Position = physicalOffset;
        var read = fs.Read(bytes, 0, length);
        if (read < length) Array.Resize(ref bytes, read);
        return bytes;
    }

    private SortedSet<int> CollectEntryPoints(SegmentModel segment, byte[] bytes)
    {
        var entryPoints = new SortedSet<int>();
        var segNum = (int)segment.SegmentNumber;

        var mainCs = (int)(_dump.NeHeader.NE_CsIp >> 16);
        var mainIp = (int)(_dump.NeHeader.NE_CsIp & 0xFFFF);
        if (segNum == mainCs && mainIp >= 0 && mainIp < bytes.Length)
            entryPoints.Add(mainIp);

        foreach (var ep in _dump.EntryBundles.SelectMany(b => b.EntryPoints))
        {
            if (ep.Segment != segNum) continue;
            if (ep.Offset < bytes.Length)
                entryPoints.Add(ep.Offset);
        }

        // Seed fixup targets
        if (_fixupTargetsBySegment.TryGetValue(segNum, out var refs))
            foreach (var r in refs.Where(r => r >= 0 && r < bytes.Length))
                entryPoints.Add(r);

        return entryPoints;
    }

    private (IReadOnlyList<DecodedInstruction> instructions, HashSet<int> jumpTableBytes)
        RunAnalysisLoop(byte[] bytes, SortedSet<int> seeds, int segNum)
    {
        IReadOnlyList<DecodedInstruction> instrs = [];
        var jumpTableBytes = new HashSet<int>();

        for (var pass = 0; pass < MaxAnalysisPasses; pass++)
        {
            (instrs, _) = DecodeBytes(bytes, [.. seeds]);

            var reachable = RunRecursiveDescent(instrs, seeds, bytes, jumpTableBytes);
            var addedNew = DiscoverJumpTableTargets(
                instrs, reachable, bytes, Is32Bit, seeds, jumpTableBytes);

            if (!addedNew) break;
        }

        return (instrs, jumpTableBytes);
    }

    private (IReadOnlyList<DecodedInstruction> instructions, SortedSet<int> entries)
        DecodeBytes(byte[] bytes, int[] entryPoints)
    {
        var opcodeMap = Is32Bit ? ExtendedOpcodeMap : OpcodeMap;
        var state = Decoder.create(opcodeMap, InterruptMap, _mode);
        var (list, _, _) = Decoder.runRecursive(state, bytes, entryPoints);
        return (list, []);
    }

    private static HashSet<int> RunRecursiveDescent(
        IReadOnlyList<DecodedInstruction> instructions,
        IEnumerable<int> seeds,
        byte[] bytes,
        HashSet<int> jumpTableBytes)
    {
        var instructionAt = new Dictionary<int, DecodedInstruction>(instructions.Count);
        foreach (var instr in instructions)
            instructionAt.TryAdd(instr.Offset, instr);

        var reachable = new HashSet<int>();
        var worklist = new Stack<int>();

        foreach (var seed in seeds) worklist.Push(seed);

        while (worklist.Count > 0)
        {
            var offset = worklist.Pop();
            if (jumpTableBytes.Contains(offset)) continue;
            if (IsZeroPadding(bytes, offset)) continue;
            if (!reachable.Add(offset)) continue;
            if (!instructionAt.TryGetValue(offset, out var instr)) continue;

            if (instr.Flow.IsReturn || instr.Flow.IsTerminate || instr.Flow.IsHalt)
                continue;

            foreach (var target in instr.Targets)
                if (target >= 0 && instructionAt.ContainsKey(target))
                    worklist.Push(target);

            if (instr.Flow.IsIndirectJump || instr.Flow.IsJump || instr.Flow.IsIndirectJumpFar)
                continue;

            var next = offset + instr.Length;
            if (instructionAt.ContainsKey(next))
                worklist.Push(next);
        }

        return reachable;
    }

    private static bool IsZeroPadding(byte[] bytes, int offset, int count = 4)
    {
        if (offset < 0 || offset + count > bytes.Length) return false;
        for (var i = 0; i < count; i++)
            if (bytes[offset + i] != 0)
                return false;
        return true;
    }

    private static bool DiscoverJumpTableTargets(
        IReadOnlyList<DecodedInstruction> instructions,
        HashSet<int> reachable,
        byte[] bytes,
        bool is32Bit,
        SortedSet<int> entryPoints,
        HashSet<int> jumpTableBytes)
    {
        var ptrSize = is32Bit ? 4 : 2;
        var addedAny = false;

        var knownCodeOffsets = new SortedSet<int>(entryPoints);
        foreach (var off in reachable) knownCodeOffsets.Add(off);

        foreach (var instr in instructions)
        {
            if (!reachable.Contains(instr.Offset)) continue;
            if (!instr.Flow.IsIndirectJump) continue;

            var memOp = instr.Operands.FirstOrDefault(o => o.Part is OperandPart.Mem);
            if (memOp?.Part is not OperandPart.Mem mem) continue;

            if (!TryExtractHexDisplacement(mem.Item, out var tableAddr)) continue;
            if (tableAddr <= 0 || tableAddr + ptrSize > bytes.Length) continue;

            var upperBound = bytes.Length;
            var nextCode = knownCodeOffsets.GetViewBetween(tableAddr + 1, bytes.Length).FirstOrDefault();
            if (nextCode > tableAddr) upperBound = nextCode;

            var pos = tableAddr;
            var localTargets = new List<int>();

            while (pos + ptrSize <= upperBound)
            {
                var target = is32Bit
                    ? BitConverter.ToInt32(bytes, pos)
                    : BitConverter.ToUInt16(bytes, pos);

                if (target <= 0 || target >= bytes.Length || IsZeroPadding(bytes, target))
                    break;

                localTargets.Add(target);
                pos += ptrSize;

                if (target > tableAddr && target < upperBound)
                    upperBound = target;
            }

            if (localTargets.Count < 2) continue;

            for (var b = tableAddr; b < pos; b++) jumpTableBytes.Add(b);

            foreach (var t in localTargets)
                if (entryPoints.Add(t))
                {
                    knownCodeOffsets.Add(t);
                    addedAny = true;
                }
        }

        return addedAny;
    }

    private static bool TryExtractHexDisplacement(string memText, out int displacement)
    {
        displacement = 0;
        var idx = memText.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var start = idx + 2;
        var end = start;
        while (end < memText.Length && Uri.IsHexDigit(memText[end])) end++;
        if (end == start) return false;

        return int.TryParse(
            memText[start..end],
            System.Globalization.NumberStyles.HexNumber,
            null,
            out displacement);
    }

    private void BuildSegmentLocalLabels(int segNum, IReadOnlyList<DecodedInstruction> instructions)
    {
        var keptOffsets = new HashSet<int>(instructions.Select(i => i.Offset));

        foreach (var instr in instructions)
        {
            var isFar = instr.Flow.IsIndirectCallFar || instr.Flow.IsIndirectJumpFar;
            foreach (var target in instr.Targets)
            {
                if (target < 0) continue;
                if (isFar && target == 0) continue;
                if (!keptOffsets.Contains(target)) continue;

                var key = (segNum, target);
                if (_exportAt.ContainsKey(key)) continue;
                if (_localLabelAt.ContainsKey(key)) continue;

                _localLabelAt[key] = MakeLabel(instr, target);
            }
        }

        return;

        string MakeLabel(DecodedInstruction instruction, int target)
        {
            if (instruction.Flow.IsIndirectCallFar)
                return $"{ProcedurePrefix}_far_{target:X8}";
            if (instruction.Flow.IsIndirectJumpFar)
                return $"{LabelPrefix}_far_{target:X8}";

            return $"{LabelPrefix}_0x{target:X8}";
        }
    }

    private string? ResolveLabel(int segNum, int offset)
    {
        var mainCs = (int)(_dump.NeHeader.NE_CsIp >> 16);
        var mainIp = (int)(_dump.NeHeader.NE_CsIp & 0xFFFF);

        if (segNum == mainCs && offset == mainIp) return "main";

        var exportLabel = _exportAt.TryGetValue((segNum, offset), out var exportName)
            ? exportName
            : _localLabelAt.GetValueOrDefault((segNum, offset));

        return exportLabel;
    }

    private List<AnalysedInstruction> AnalyseInstructions(
        int segNum,
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
            var label = ResolveLabel(segNum, instr.Offset);
            var instrFixups = CollectFixupsForRange(segNum, instr.Offset, instr.Length);
            var analysedOps = new List<AnalysedOperand>(instr.Operands.Length);

            foreach (var op in instr.Operands)
            {
                var opFixup = FindFixupForOperand(instr.Offset, op, instrFixups);
                var text = FormatOperandWithSubstitution(segNum, instr, op, opFixup);
                analysedOps.Add(new AnalysedOperand(text));
            }

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

    private List<FixupSite> CollectFixupsForRange(int segNum, int instrOffset, int instrLength)
    {
        var result = new List<FixupSite>();

        // Scan a small window before to catch straddled multi-byte fixups
        const int lookbackMax = 16;
        var scanStart = Math.Max(0, instrOffset - lookbackMax);
        var scanEnd = instrOffset + instrLength;

        for (var i = scanStart; i < scanEnd; i++)
        {
            if (!_fixupAt.TryGetValue((segNum, i), out var fixup)) continue;
            var fEnd = fixup.Offset + fixup.Size;
            if (fEnd > instrOffset && fixup.Offset < scanEnd)
                result.Add(fixup);
        }

        return result;
    }

    private static FixupSite? FindFixupForOperand(
        int instrOffset, DecodedOperand op, IReadOnlyList<FixupSite> instrFixups)
    {
        if (op.BytesLength <= 0 || instrFixups.Count == 0) return null;

        var opStart = instrOffset + op.BytesOffset;
        var opEnd = opStart + op.BytesLength;

        // Prefer fixup starting exactly at the operand start
        foreach (var f in instrFixups)
            if (f.Offset == opStart)
                return f;

        // Otherwise first overlap
        return (
            from f in instrFixups 
            let fStart = f.Offset 
            let fEnd = fStart + f.Size 
            where fStart < opEnd && fEnd > opStart 
            select f
        ).FirstOrDefault();
    }

    private string FormatOperandWithSubstitution(
        int segNum, DecodedInstruction instr, DecodedOperand op, FixupSite? fixup)
    {
        // Far operands need special care - combine seg from fixup with off from code
        if (op.Part is OperandPart.Far far)
            return FormatFarOperand(segNum, far, fixup);

        if (fixup is not null)
        {
            return op.Part switch
            {
                OperandPart.Imm => fixup.Symbol,
                OperandPart.SignedImm => fixup.Symbol,
                OperandPart.Abs => $"[{fixup.Symbol}]",
                OperandPart.Mem mem => ReplaceMemDisplacement(mem.Item, fixup.Symbol),
                _ => FormatOperandPlain(instr, op)
            };
        }

        // Intra-segment branch/call target
        if (instr.Targets.Count() == 1 && op.Part is OperandPart.Imm or OperandPart.SignedImm)
        {
            var target = instr.Targets[0];
            if (target >= 0 && _localLabelAt.TryGetValue((segNum, target), out var localLabel))
                return localLabel;
        }

        return FormatOperandPlain(instr, op);
    }

    /// <summary>
    /// Resolves a far operand. Handles three linker patterns:
    ///   A) AddressType=2 (selector): offset from code bytes, segment# from fixup
    ///   B) AddressType=3/11 (16:16 / 16:32): both from fixup record
    ///   C) AddressType=5/13 (just offset): offset from fixup, segment# from code bytes
    /// </summary>
    private string FormatFarOperand(int segNum, OperandPart.Far far, FixupSite? fixup)
    {
        var codeSeg = unchecked((int)far.Item1);
        var codeOffset = unchecked((int)far.Item2);

        if (fixup is null)
        {
            if (_exportAt.TryGetValue((codeSeg, codeOffset), out var bare))
                return bare;
            if (_fixupAt.TryGetValue((segNum, codeOffset), out var nested))
                return nested.Symbol;
            return $"0x{codeSeg:X4}:{codeOffset:X4}";
        }

        // Import / unresolved -> emit symbol name directly
        if (fixup.TargetSegment == 0)
            return fixup.Symbol;

        int targetSeg, targetOff;

        if (fixup.IsSelectorOnly)
        {
            // Pattern A: offset is real in code bytes, selector patched by fixup
            targetSeg = fixup.TargetSegment;
            targetOff = codeOffset + fixup.Additive;
        }
        else
        {
            // Patterns B & C: offset comes from fixup (reloc record authoritative)
            targetSeg = fixup.TargetSegment;
            targetOff = fixup.TargetOffset + fixup.Additive;
        }

        if (_exportAt.TryGetValue((targetSeg, targetOff), out var name))
            return name;
        if (_localLabelAt.TryGetValue((targetSeg, targetOff), out var localLabel))
            return localLabel;

        return $"0x{targetSeg:X4}:{targetOff:X4}";
    }

    private string FormatOperandPlain(DecodedInstruction instr, DecodedOperand op) =>
        Decoder.formatOperandPart(_mode, instr.Offset, op.Part);

    private static string ReplaceMemDisplacement(string memText, string symbol)
    {
        var plus = memText.LastIndexOf('+');
        var minus = memText.LastIndexOf('-');
        var cut = Math.Max(plus, minus);

        if (cut <= 0) return $"[{symbol}]";
        return $"{memText[..cut]}+{symbol}]";
    }

    private void RenderSegment(
        int segNum, byte[] bytes, IReadOnlyList<AnalysedInstruction> analysed)
    {
        var sb = new StringBuilder();
        var cover = 0;
        string? currentProcName = null;

        var ordered = analysed.OrderBy(a => a.Instr.Offset).ToArray();

        for (var idx = 0; idx < ordered.Length; idx++)
        {
            var ai = ordered[idx];
            var instr = ai.Instr;
            var startsNewProc = ai.IsEntryPoint || ai.IsCallTarget;

            if ((startsNewProc || instr.Offset > cover) && currentProcName is not null)
            {
                sb.AppendLine($"{currentProcName} ENDP");
                currentProcName = null;
            }

            if (instr.Offset > cover)
                RenderDataRange(sb, segNum, bytes, cover, instr.Offset - cover);

            cover = Math.Max(cover, instr.Offset + instr.Length);

            if (startsNewProc)
            {
                var procName = ai.Label ?? $"{ProcedurePrefix}_0x{segNum:X4}{instr.Offset:X4}";

                sb.AppendLine(";");
                var header = (ai.IsEntryPoint, ai.IsCallTarget) switch
                {
                    (true, true) => $"; Export CALL target @ 0x{instr.Offset:X4}",
                    (true, false) => $"; Export @ 0x{instr.Offset:X4}",
                    _ => $"; Procedure at 0x{instr.Offset:X4}"
                };
                sb.AppendLine(header);
                sb.AppendLine(";");

                var distance = Is32Bit
                    ? string.Empty
                    : _exportAt.ContainsKey((segNum, instr.Offset)) || ai.Label == "main"
                        ? " FAR"
                        : " NEAR";

                sb.AppendLine($"{procName} PROC{distance}");
                currentProcName = procName;
            }
            else if (ai.Label is not null)
            {
                sb.AppendLine($"{ai.Label}:");
            }

            var operandText = string.Join(", ", ai.Operands.Select(o => o.Text).Where(t => t.Length > 0));
            var fullText = operandText.Length > 0 ? $"{instr.Mnemonic} {operandText}" : instr.Mnemonic;
            var bytesStr = BitConverter.ToString(instr.Bytes).Replace("-", " ");

            var xrefComment = ai.AllFixups.Count > 0
                ? " xref: " + string.Join(", ", ai.AllFixups.Select(f =>
                    f.Offset == instr.Offset
                        ? f.Symbol
                        : $"{f.Symbol}+0x{f.Offset - instr.Offset:X}"))
                : string.Empty;

            var suffixComment = instr.Comment is not null && instr.Comment.Value.Length > 0
                ? instr.Comment.Value
                : string.Empty;

            sb.AppendLine($"\t{fullText,-30} ; {segNum}:0x{instr.Offset:X4} {bytesStr}{suffixComment}{xrefComment}");

            if (instr.Flow.IsReturn)
            {
                var ctx = currentProcName is not null ? $"{currentProcName} " : "previous ";
                sb.AppendLine($"; returned back to {ctx}control flow");
            }
            else if (instr.Flow.IsTerminate)
            {
                sb.AppendLine($"; Terminated ({suffixComment})");
            }
            else if (instr.Flow.IsHalt)
            {
                sb.AppendLine("; System Halted");
            }
        }

        if (currentProcName is not null)
            sb.AppendLine($"{currentProcName} ENDP");

        if (cover < bytes.Length)
            RenderDataRange(sb, segNum, bytes, cover, bytes.Length - cover);

        _results.Add(sb.ToString());
    }

    private static void RenderDataRange(
        StringBuilder sb, int segNum, byte[] bytes, int offset, int length)
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
                              $" ; {segNum}:0x{runStart:X4} padding");
                continue;
            }

            var pos = runStart;
            while (pos < mutableI && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, mutableI - pos);
                var payload = string.Join(", ", bytes[pos..(pos + take)].Select(b => $"0x{b:X2}"));
                sb.AppendLine($"\tDB {payload},".PadRight(30) + $" ; {segNum}:0x{pos:X4}");
                pos += take;
                linesLeft--;
            }
        }

        if (mutableI < limit)
            sb.AppendLine($"; {segNum}:0x{mutableI:X4} {limit - mutableI} more byte(s) of data (omitted)");
    }

    private void DumpSegmentStrings(int segNum, byte[] bytes)
    {
        if (!DumpStrings) return;

        var strings = CollectAsciiStrings(bytes, MinStringLength);
        if (strings.Count == 0) return;

        _results.Add("");
        _results.Add($"; --- ASCII strings in segment #{segNum} ({strings.Count}) ---");
        foreach (var (offset, text) in strings)
            _results.Add($"\tDB \"{text}\" ; {segNum}:0x{offset:X4} len={text.Length}");
    }

    private static List<(int offset, string text)> CollectAsciiStrings(byte[] bytes, int minLength)
    {
        var result = new List<(int, string)>();
        var start = -1;
        var sb = new StringBuilder();

        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b >= 0x20 && b < 0x7F)
            {
                if (start < 0)
                {
                    start = i;
                    sb.Clear();
                }

                sb.Append((char)b);
            }
            else
            {
                if (sb.Length >= minLength) result.Add((start, sb.ToString()));
                start = -1;
                sb.Clear();
            }
        }

        if (sb.Length >= minLength) result.Add((start, sb.ToString()));
        return result;
    }

    private void EmitSegmentHeader(
        int segNum, SegmentModel segment,
        int instrCount, int seedCount, int exportCount, int byteCount)
    {
        var modeLabel = Is32Bit ? "32-bit" : "16-bit";
        var useDir = Is32Bit ? "USE32" : "USE16";

        _results.Add(";");
        _results.Add($";          Segment#{segNum} ({segment.Type})");
        _results.Add($"; File offset: 0x{segment.FileOffset:X4} × {1 << _alignmentShift}");
        _results.Add($"; Size:        {byteCount} bytes");
        _results.Add($"; Instructions: {instrCount}");
        _results.Add($"; Seeds:        {seedCount}");
        _results.Add($"; Exports:      {exportCount}");
        _results.Add($"; Mode:         {modeLabel}");
        _results.Add(";");
        _results.Add($"SEGMENT Segment_{segNum} {useDir} ; {segment.Type}");
        _results.Add("");
    }

    private void EmitSegmentFooter(int segNum)
    {
        _results.Add("");
        _results.Add($"ENDS Segment_{segNum}");
        _results.Add("");
    }
}