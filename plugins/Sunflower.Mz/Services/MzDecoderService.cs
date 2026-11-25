// CoffeeLake (C) 2026-*
// Recursive-descent disassembler for MS-DOS real-mode programs
// Mirrors the analysis pipeline from DecoderServiceBase, but:
//   - single "object" (whole load module), objNumber always 0
//   - no exports / no import records / no fixup pages
//   - relocations are word-offsets inside the load module (see BuildRelocationSet)
//   - addresses inside memory operands & far pointers are *runtime* offsets,
//     translated via _fileBase (loadModuleBase for MZ, -0x100 for COM)
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
using System.Globalization;
using System.Text;
using Microsoft.FSharp.Core;
using Sunflower.Dasm.Intel.Core;
using Sunflower.Mz.Models;
using Decoder = Sunflower.Dasm.Intel.Core.Decoder;

namespace Sunflower.Mz.Services;

public sealed class MzDecoderService
{
    public bool DumpStrings { get; set; } = true;
    public bool DiscoverDataProcedures { get; set; } = true;
    public int MinStringLength { get; set; } = 4;

    private const int MaxPasses = 8;

    private readonly byte[] _bytes;
    private readonly long _fileBase;     // file offset of runtime offset 0
    private readonly int _codeStartFile; // file offset of entry point
    private readonly ProcessorMode _mode;
    private readonly string _opcodeMapPath;
    private readonly string _interruptMapPath;
    private readonly string _memoryModel;

    private readonly HashSet<long> _relocWordFileOffsets = [];

    // label registry (file-offset keyed, because instr.Offset is file offset)
    private readonly Dictionary<int, string> _labels = [];

    private readonly List<string> _results = [];

    private MzDecoderService(
        byte[] bytes,
        long fileBase,
        int codeStartFile,
        ProcessorMode mode,
        string opcodeMapPath,
        string interruptMapPath,
        string memoryModel,
        IEnumerable<MzRelocation> relocations)
    {
        _bytes = bytes;
        _fileBase = fileBase;
        _codeStartFile = codeStartFile;
        _mode = mode;
        _opcodeMapPath = opcodeMapPath;
        _interruptMapPath = interruptMapPath;
        _memoryModel = memoryModel;
        // reloc.Position is in load-module space
        // recomputes as raw offset
        foreach (var r in relocations)
        {
            var f = _fileBase + (long)r.Segment * 16 + r.Offset;
            if (f >= 0 && f + 2 <= _bytes.Length)
                _relocWordFileOffsets.Add(f);
        }
        
    }
    
    public static MzDecoderService ForMz(
        string path, MzDumpManager mgr, ProcessorMode mode,
        string opcodeMapPath, string interruptMapPath)
    {
        var bytes = File.ReadAllBytes(path);
        var fileBase = mgr.Header.e_cparhdr * 16L;
        var codeStart = (int)mgr.CodePointer;
        return new MzDecoderService(bytes, fileBase, codeStart, mode,
            opcodeMapPath, interruptMapPath,"SMALL", mgr.Relocations);
    }

    public static MzDecoderService ForCom(
        string path, ProcessorMode mode,
        string opcodeMapPath, string interruptMapPath)
    {
        var bytes = File.ReadAllBytes(path);
        // load module is file itself
        // runtime base is PSP=0x100 above file base.
        return new MzDecoderService(bytes, -0x100L, 0, mode,
            opcodeMapPath, interruptMapPath, "TINY",[]);
    }
    
    public string[] Analyse()
    {
        EmitHeader();

        var seeds = new SortedSet<int> { _codeStartFile };
        AddFarCallPeekSeeds(seeds);
        AddDataPointerSeeds(seeds); // callbacks / vtables in .DATA
        
        if (DiscoverDataProcedures) 
            AddPrologueSeeds(seeds);

        var (instructions, jumpTableBytes) = RunAnalysisLoop(seeds);
        var reachable = RunRecursiveDescent(instructions, seeds, jumpTableBytes);

        var kept = instructions
            .Where(i => reachable.Contains(i.Offset))
            .OrderBy(i => i.Offset)
            .ToArray();

        BuildLocalLabels(kept);
        var callTargets = ComputeCallTargets(kept);

        RenderCode(kept, seeds, callTargets);

        if (DumpStrings) RenderStrings();

        EmitFooter();
        return _results.ToArray();
    }
    
    private void EmitHeader()
    {
        _results.AddRange([
            $";        This file is **generated** at {DateTime.UtcNow:u}UTC ",
            $"; Processed by SunFlower.Mz.dll; CoffeeLake (C) 2026-*",
            $"; CPU Instruction set:    {_mode}",
            $"; Load ImageBase:         0x{_fileBase:X}",
            $"; Entry Point:            0x{_codeStartFile:X4}",
            $"; Total Relocations:      {_relocWordFileOffsets.Count}",
            "",
            $".MODEL {_memoryModel}",
            ""
        ]);
    }

    private void EmitFooter() => _results.Add("END");
    
    /// <summary>
    /// (Should work before decoding process started)
    /// Peeks at file bytes for far call/jump patterns
    /// where the segment word ww is covered by a relocation record.
    /// </summary>
    private void AddFarCallPeekSeeds(SortedSet<int> seeds)
    {
        foreach (var w in _relocWordFileOffsets)
        {
            if (w < 4) 
                continue;
            var opcode = _bytes[w - 4];
            
            if (opcode != 0x9A && opcode != 0xEA) 
                continue;

            var off = (ushort)(_bytes[w - 2] | (_bytes[w - 1] << 8));
            var target = _fileBase + off;
            
            if (target >= _codeStartFile && target < _bytes.Length)
                seeds.Add((int)target);
        }
    }

    /// <summary>
    /// Scans relocation words that look like the segment half of a far pointer
    /// in .DATA: previous word = offset, this word = segment (usually 0 in file).
    /// If translated target lands in code range, add it as a seed.
    /// </summary>
    private void AddDataPointerSeeds(SortedSet<int> seeds)
    {
        foreach (var w in _relocWordFileOffsets)
        {
            if (w < 2 || w + 2 > _bytes.Length) continue;

            // Skip if this is clearly the tail of a far CALL/JMP
            var offsetFile = w - 2;
            if (offsetFile >= 1 && (_bytes[offsetFile - 1] == 0x9A || _bytes[offsetFile - 1] == 0xEA))
                continue;

            var off = (ushort)(_bytes[offsetFile] | (_bytes[offsetFile + 1] << 8));
            var seg = (ushort)(_bytes[w] | (_bytes[w + 1] << 8));
            var target = _fileBase + (long)seg * 16 + off;
            if (target < _codeStartFile || target >= _bytes.Length) continue;

            seeds.Add((int)target);
        }
    }

    private void AddPrologueSeeds(SortedSet<int> seeds)
    {
        // Typical 16-bit prologues: 55 8B EC (push bp; mov bp,sp) / 55 89 E5 (AT&T LOL)
        for (var i = 0; i + 3 < _bytes.Length; i++)
        {
            var b0 = _bytes[i];
            var b1 = _bytes[i + 1];
            var b2 = _bytes[i + 2];
            var isPrologue = b0 == 0x55 &&
                             ((b1 == 0x8B && b2 == 0xEC) || (b1 == 0x89 && b2 == 0xE5));
            if (isPrologue) seeds.Add(i);
        }
    }

    private (IReadOnlyList<DecodedInstruction> instrs, HashSet<int> jumpTableBytes)
        RunAnalysisLoop(SortedSet<int> seeds)
    {
        IReadOnlyList<DecodedInstruction> instrs = [];
        var jumpTableBytes = new HashSet<int>();
        var decoderState = Decoder.create(_opcodeMapPath, _interruptMapPath, _mode);

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var (decoded, _, _) = Decoder.runRecursive(decoderState, _bytes, [.. seeds]);
            if (decoded is null) break;
            instrs = decoded;

            var added = DiscoverJumpTableTargets(instrs, seeds, jumpTableBytes);
            added |= AddFarCallSeeds(instrs, seeds);
            if (!added) break;
        }

        return (instrs, jumpTableBytes);
    }

    private bool AddFarCallSeeds(IEnumerable<DecodedInstruction> instrs, SortedSet<int> seeds)
    {
        var added = false;
        foreach (var instr in instrs)
        {
            if (!(instr.Flow.Equals(FlowControl.Call)
                  || instr.Flow.IsIndirectCallFar
                  || instr.Flow.IsIndirectJumpFar))
                continue;

            foreach (var op in instr.Operands)
            {
                if (op.Part is not OperandPart.Far far) 
                    continue;
                
                var target = _fileBase + unchecked((int)far.Item2);
                if (target >= _codeStartFile && target < _bytes.Length && seeds.Add((int)target))
                    added = true;
            }
        }
        return added;
    }

    /// <summary>
    /// Discovers the 16-bit jump table
    /// Also accept absolute-offset tables as a fallback (some old linkers might emit those).
    /// </summary>
    private bool DiscoverJumpTableTargets(
        IReadOnlyList<DecodedInstruction> instrs,
        SortedSet<int> seeds,
        HashSet<int> jumpTableBytes)
    {
        var addedAny = false;
        var known = new SortedSet<int>(seeds);

        foreach (var instr in instrs)
        {
            if (!instr.Flow.IsIndirectJump) continue;

            var memOp = instr.Operands.FirstOrDefault(o => o.Part is OperandPart.Mem);
            if (memOp?.Part is not OperandPart.Mem mem) continue;
            if (!TryExtractHexDisplacement(mem.Item, out var dispRuntime)) continue;

            // disp is a DS-relative runtime offset
            var tableFile = _fileBase + dispRuntime;
            if (tableFile < 0 || tableFile + 2 > _bytes.Length) continue;

            var pos = (int)tableFile;
            var localTargets = new List<int>();
            var ptrSize = 2;
            var upperBound = _bytes.Length;
            var nextCode = known.GetViewBetween((int)tableFile + 1, _bytes.Length).FirstOrDefault();
            if (nextCode > tableFile) upperBound = nextCode;

            while (pos + ptrSize <= upperBound)
            {
                var raw = BitConverter.ToUInt16(_bytes, pos);
                var candidate = _fileBase + raw;
                long resolved = -1;

                if (candidate >= _codeStartFile && candidate < _bytes.Length)
                    resolved = candidate;
                else if (raw >= _codeStartFile && raw < _bytes.Length)
                    resolved = raw;   // fallback: absolute-offset table

                if (resolved < 0) break;
                if (resolved > tableFile && resolved < upperBound) upperBound = (int)resolved;

                localTargets.Add((int)resolved);
                pos += ptrSize;
            }

            if (localTargets.Count < 2) continue;

            for (var b = tableFile; b < pos; b++) jumpTableBytes.Add((int)b);
            foreach (var t in localTargets)
                if (seeds.Add(t)) { known.Add(t); addedAny = true; }
        }

        return addedAny;
    }

    private static bool TryExtractHexDisplacement(string memText, out int disp)
    {
        disp = 0;
        var idx = memText.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var start = idx + 2;
        var end = start;
        while (end < memText.Length && Uri.IsHexDigit(memText[end])) end++;
        if (end == start) return false;
        return int.TryParse(memText[start..end],
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out disp);
    }

    private static HashSet<int> RunRecursiveDescent(
        IReadOnlyList<DecodedInstruction> instructions,
        IEnumerable<int> seeds,
        HashSet<int> jumpTableBytes)
    {
        var at = new Dictionary<int, DecodedInstruction>(instructions.Count);
        foreach (var i in instructions) at.TryAdd(i.Offset, i);

        var reachable = new HashSet<int>();
        var stack = new Stack<int>();
        foreach (var s in seeds) stack.Push(s);

        while (stack.Count > 0)
        {
            var off = stack.Pop();
            if (jumpTableBytes.Contains(off)) continue;
            if (!reachable.Add(off)) continue;
            if (!at.TryGetValue(off, out var instr)) continue;
            if (instr.Flow.IsReturn || instr.Flow.IsTerminate || instr.Flow.IsHalt) continue;

            foreach (var t in instr.Targets)
                if (t >= 0 && at.ContainsKey(t)) stack.Push(t);

            if (IsUnconditionalBranch(instr)) continue;
            var next = off + instr.Length;
            if (at.ContainsKey(next)) stack.Push(next);
        }

        return reachable;
    }

    private static bool IsUnconditionalBranch(DecodedInstruction i) =>
        i.Flow.IsJump || i.Flow.IsIndirectJump || i.Flow.IsIndirectJumpFar;

    private void BuildLocalLabels(IReadOnlyList<DecodedInstruction> instructions)
    {
        var kept = new HashSet<int>(instructions.Select(i => i.Offset));
        foreach (var instr in instructions)
        {
            foreach (var target in instr.Targets)
            {
                if (target < 0 || !kept.Contains(target)) continue;
                if (_labels.ContainsKey(target)) continue;
                _labels[target] = $"label_0x{target:X4}";
            }
        }
    }

    private HashSet<int> ComputeCallTargets(IReadOnlyList<DecodedInstruction> instructions)
    {
        var set = new HashSet<int>();
        foreach (var instr in instructions)
        {
            if (!(instr.Flow.Equals(FlowControl.Call) || instr.Flow.IsIndirectCallFar))
                continue;

            foreach (var t in instr.Targets)
            {
                var translated = TranslateTarget(t);
                if (translated >= _codeStartFile && translated < _bytes.Length)
                    set.Add(translated);
            }

            // direct far calls: add from Far operand explicitly
            foreach (var op in instr.Operands)
                if (op.Part is OperandPart.Far far)
                {
                    var target = _fileBase + unchecked((int)far.Item2);
                    if (target >= _codeStartFile && target < _bytes.Length)
                        set.Add((int)target);
                }
        }
        return set;
    }

    private int TranslateTarget(int raw)
    {
        if (raw >= _codeStartFile && raw < _bytes.Length) return raw;
        var translated = _fileBase + raw;
        return translated >= _codeStartFile && translated < _bytes.Length ? (int)translated : raw;
    }

    private void RenderCode(
        IReadOnlyList<DecodedInstruction> instructions,
        SortedSet<int> seeds,
        HashSet<int> callTargets)
    {
        var cover = 0;
        string? currentProc = null;

        foreach (var instr in instructions)
        {
            var startsNewProc = seeds.Contains(instr.Offset) || callTargets.Contains(instr.Offset);

            if ((startsNewProc || instr.Offset > cover) && currentProc is not null)
            {
                _results.Add($"{currentProc} ENDP");
                _results.Add("");
                currentProc = null;
            }

            if (instr.Offset > cover)
                RenderDataRange(cover, instr.Offset - cover);

            cover = Math.Max(cover, instr.Offset + instr.Length);

            if (startsNewProc)
            {
                var name = instr.Offset == _codeStartFile
                    ? DecoderFactory.MainProcedureLabel // <-- finally we have an entry point
                    : _labels.TryGetValue(instr.Offset, out var lbl)
                        ? lbl
                        : $"procedure_0x{instr.Offset:X4}";
                _results.Add($"{name} PROC NEAR");
                currentProc = name;
            }
            else if (_labels.TryGetValue(instr.Offset, out var label))
            {
                _results.Add($"{label}:");
            }

            var operandText = FormatOperands(instr);
            var fullText = operandText.Length > 0
                ? $"{instr.Mnemonic} {operandText}"
                : instr.Mnemonic;
            var bytesStr = BitConverter.ToString(instr.Bytes).Replace("-", " ");
            var notes = BuildAnnotation(instr);

            var comment = FSharpOption<string>.get_IsNone(instr.Comment) 
                ? "" 
                : instr.Comment.Value; 
            
            _results.Add($"\t{fullText,-30} ; 0x{instr.Offset:X4} {bytesStr}{notes} {comment}");

            if (instr.Flow.IsReturn)
                _results.Add($"; returned to {currentProc ?? "caller"}");
            else if (instr.Flow.IsTerminate)
                _results.Add("; terminated");
        }

        if (currentProc is not null)
        {
            _results.Add($"{currentProc} ENDP");
            _results.Add("");
        }

        if (cover < _bytes.Length)
            RenderDataRange(cover, _bytes.Length - cover);
    }

    private string FormatOperands(DecodedInstruction instr)
    {
        var parts = new List<string>();
        foreach (var op in instr.Operands)
        {
            var text = Decoder.formatOperandPart(_mode, instr.Offset, op.Part);

            if (op.Part is OperandPart.Far far && !string.IsNullOrEmpty(text))
            {
                var target = _fileBase + unchecked((int)far.Item2);
                if (_labels.TryGetValue((int)target, out var lbl)) text = lbl;
            }

            parts.Add(text);
        }
        return string.Join(", ", parts.Where(p => p.Length > 0));
    }

    private string BuildAnnotation(DecodedInstruction instr)
    {
        var notes = new List<string>();

        for (long p = instr.Offset; p < instr.Offset + instr.Length; p++)
        {
            if (_relocWordFileOffsets.Contains(p))
            {
                notes.Add($"reloc@0x{p:X4}");
                break;
            }
        }

        if (instr.Flow.IsIndirectCallFar || instr.Flow.IsIndirectJumpFar)
        {
            var far = instr.Operands.FirstOrDefault(o => o.Part is OperandPart.Far);
            if (far?.Part is OperandPart.Far f)
            {
                var target = _fileBase + unchecked((int)f.Item2);
                notes.Add($"far->file 0x{target:X4}");
            }
        }

        return notes.Count > 0 ? " ; " + string.Join(", ", notes) : "";
    }

    private void RenderDataRange(int offset, int length)
    {
        const int zeroThreshold = 4;
        const int bytesPerLine = 16;
        const int maxLines = 32;

        var i = offset;
        var limit = offset + length;
        var linesLeft = maxLines;

        while (i < limit && linesLeft > 0)
        {
            var runStart = i;
            var b = _bytes[i];
            while (i < limit && _bytes[i] == b) i++;
            var runLen = i - runStart;

            if (b == 0 && runLen >= zeroThreshold)
            {
                _results.Add($"\tDB {runLen} DUP (0x00),".PadRight(30) + $" ; 0x{runStart:X4} padding");
                continue;
            }

            var pos = runStart;
            while (pos < i && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, i - pos);
                var payload = string.Join(", ", _bytes[pos..(pos + take)].Select(x => $"0x{x:X2}"));
                _results.Add($"\tDB {payload},".PadRight(30) + $" ; 0x{pos:X4}");
                pos += take;
                linesLeft--;
            }
        }

        if (i < limit)
            _results.Add($"; 0x{i:X4} {limit - i} more byte(s) omitted");
    }

    private void RenderStrings()
    {
        var seen = new HashSet<string>();
        _results.Add("");
        _results.Add("; --- ASCII strings ---");
        for (var i = 0; i < _bytes.Length - MinStringLength; i++)
        {
            if (_bytes[i] < 0x20 || _bytes[i] > 0x7E) continue;
            var j = i;
            while (j < _bytes.Length && _bytes[j] >= 0x20 && _bytes[j] <= 0x7E) j++;
            var len = j - i;
            if (len < MinStringLength) { i = j; continue; }

            var text = Encoding.ASCII.GetString(_bytes, i, len);
            if (seen.Add(text))
                _results.Add($"\tDB {"\"" + text + "\"",-40} ; 0x{i:X4} len={len}");
            i = j;
        }
    }
}