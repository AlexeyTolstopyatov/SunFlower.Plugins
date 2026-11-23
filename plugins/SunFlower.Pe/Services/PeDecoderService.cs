// CoffeeLake (C) 2026-*
//
// PE decoder: thin layer over the Intel i386 decoder (Sunflower.Dasm).
// Follows the LE decoder architecture (DecoderServiceBase -> LeDecoderService)
// but adapted to the PE nature.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sunflower.Dasm.Intel.Core;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

internal sealed record DecodedSection(
    int Number,
    PeSection Section,
    byte[] Bytes,
    IReadOnlyList<DecodedInstruction>? Instructions,
    SortedSet<int>? EntryPoints,
    string? Error = null);

public sealed class PeDecoderService : DirectoryManager
{
    private const uint ExecutableMask = 0x20000000;
    private const string ProcedurePrefix = "procedure";
    private const string LabelPrefix = "label";

    private readonly ExportFunction[] _dumpExports = [];
    private readonly AddressTableModule[] _dumpIatModules = [];
    private readonly uint _entryPointRva;

    private readonly Dictionary<(int section, int off), string> _exportAt = [];

    private readonly string _filePath;
    private readonly Dictionary<uint, string> _iatSymbolByRva = [];
    private readonly Dictionary<ulong, string> _iatSymbolByVa = [];
    private readonly ImageDetails _info;
    private readonly bool _is64Bit;
    private readonly Dictionary<uint, string> _labelByVa = [];
    private readonly Dictionary<uint, uint> _pointerAt = [];
    private readonly uint[] _relocatedVas = [];
    private readonly ulong[] _relocatedVas64 = [];

    private readonly List<string> _results = [];
    private readonly Dictionary<uint, string> _stringAt = [];

    private uint? _imageSizeCache;

    public PeDecoderService(
        string filePath,
        PeDumpManager dump,
        PeImportsManager imports,
        PeExportsManager exports,
        PeBaseRelocationManager relocs) : base(dump.ImageDetails)
    {
        _filePath = filePath;
        _info = dump.ImageDetails;
        _is64Bit = dump.Is64Bit;

        _relocatedVas = relocs.RelocatedVas.ToArray();
        _relocatedVas64 = relocs.RelocatedVas64.ToArray();

        _entryPointRva = _info.EntryPoint;
        _dumpExports = exports.ExportTable.Functions.ToArray();

        _dumpIatModules = imports.ImportAddressTable is null
            ? []
            : imports.ImportAddressTable.Modules.ToArray();

        // Export map = (section, offset) -> name
        foreach (var exp in _dumpExports)
        {
            var loc = SectionOffset((uint)exp.Address);
            if (loc is null) continue;
            _exportAt[(loc.Value.section, loc.Value.offset)] = exp.Name;
        }

        // IAT maps = RVA and VA forms
        foreach (var mod in _dumpIatModules)
        foreach (var func in mod.Functions)
        {
            var symbol = func.IsOrdinal
                ? $"{mod.Name}::@{func.Ordinal}"
                : $"{mod.Name}::{func.Name}";

            _iatSymbolByRva[(uint)func.SlotRva] = symbol;
            _iatSymbolByVa[unchecked(_info.ImageBase + func.SlotRva)] = symbol;
        }

        BuildPointerMap();
    }

    public bool DumpStrings { get; set; } = true;
    public int MinStringLength { get; set; } = 4;


    private uint ImageSize => _imageSizeCache ??= ComputeImageSize();

    private uint ComputeImageSize()
    {
        if (_info.SizeOfImage > 0) return _info.SizeOfImage;
        return _info.Sections
            .Select(s => s.VirtualAddress + Math.Max(s.VirtualSize, s.SizeOfRawData))
            .DefaultIfEmpty(0u)
            .Max();
    }

    private bool IsInsideImage(uint va)
    {
        if (va < _info.ImageBase) return false;
        return va - _info.ImageBase < ImageSize;
    }

    /// <summary>
    ///     Every relocation record tells about absolute VA fixup via storageVA
    ///     So, this method is using Relocation WORD -> makes a pointer map -> applies it for a target
    /// </summary>
    private void BuildPointerMap()
    {
        if (_relocatedVas.Length == 0) return;

        var imageEnd = _info.ImageBase + ImageSize;

        using var fs = File.OpenRead(_filePath);
        using var br = new BinaryReader(fs);

        foreach (var storageVa in _relocatedVas)
        {
            var rva = storageVa - _info.ImageBase;
            var fileOff = Offset(rva);
            if (fileOff < 0) continue;

            try
            {
                br.BaseStream.Seek(fileOff, SeekOrigin.Begin);
                var targetVa = br.ReadUInt32();
                if (targetVa >= _info.ImageBase && targetVa < imageEnd)
                    _pointerAt[storageVa] = targetVa;
            }
            catch
            {
                /* corrupted pointers are not fatal */
            }
        }
    }

    private void BuildStringMap()
    {
        _stringAt.Clear();

        foreach (var section in _info.Sections)
        {
            var bytes = ReadSectionBytes(section);
            if (bytes is null || bytes.Length == 0) continue;

            foreach (var (offset, text) in CollectAsciiStrings(bytes, MinStringLength))
            {
                var va = _info.ImageBase + section.VirtualAddress + (uint)offset;
                _stringAt[va] = text;
            }
        }
    }

    private string? GetLabelOrNullForVirtualAddress(uint va)
    {
        if (_labelByVa.TryGetValue(va, out var lbl)) return lbl;
        if (_stringAt.TryGetValue(va, out _)) return $"str_{va:X8}";

        if (!IsInsideImage(va)) return null;

        // Default "undefined" or "unnamed" label
        return $"loc_{va:X8}";
    }

    /// <summary>
    ///     Memory cell name. <c>[0x43C640]</c> where 0x43C640 is an IAT slot
    ///     might resolve to <c>Module::Procedure</c>.
    ///     <c>[0x43B5CC]</c> -> <c>dword_43B5CC</c>.
    /// </summary>
    private string? ResolveMemoryCell(uint va)
    {
        if (_iatSymbolByVa.TryGetValue(va, out var sym)) return sym;
        if (_relocatedVas.Contains(va)) return $"dword_{va:X8}";
        if (_pointerAt.TryGetValue(va, out var target))
        {
            var targetLabel = GetLabelOrNullForVirtualAddress(target);
            return targetLabel is not null ? $"dword_{targetLabel}" : $"dword_{va:X8}";
        }

        return null;
    }

    public string[] Disassemble()
    {
        if (_entryPointRva != 0)
        {
            _results.Add(";");
            _results.Add($"; Entry point: 0x{_entryPointRva:X8}");
            _results.Add(";");
        }

        var iatCount = _dumpIatModules.Sum(m => m.Functions.Count);
        if (iatCount > 0)
        {
            _results.Add(";");
            _results.Add($"; --- Import Address Table ({iatCount}) ---");
            foreach (var mod in _dumpIatModules)
            foreach (var func in mod.Functions)
            {
                var slotVa = _info.ImageBase + (uint)func.SlotRva;
                _results.Add($"; RVA {func.SlotRva:X8}  VA {slotVa:X8}  {mod.Name}!{func.Name}" +
                             $"{(func.IsOrdinal ? $" (ordinal {func.Ordinal})" : string.Empty)}");
            }

            _results.Add(";");
        }

        if (_is64Bit)
        {
            _results.Add(";");
            _results.Add("; PE32+ (x64): code decoding is not implemented yet.");
            _results.Add(";");
            return _results.ToArray();
        }

        BuildStringMap();

        // Pass #1
        // decode all executable sections
        var orderedSections = _info.Sections.OrderBy(s => s.VirtualAddress).ToArray();
        var decoded = new List<DecodedSection>();
        for (var i = 0; i < orderedSections.Length; i++)
        {
            var section = orderedSections[i];
            if (!IsExecutable(section)) continue;

            var sectionNumber = i + 1;
            var bytes = ReadSectionBytes(section);
            if (bytes is null || bytes.Length == 0) continue;

            var entryPoints = CollectEntryPoints(sectionNumber, bytes);
            try
            {
                var (instructions, entrySet, _) = RunAnalysisLoop(bytes, entryPoints);
                decoded.Add(new DecodedSection(sectionNumber, section, bytes, instructions, entrySet));
            }
            catch (Exception ex)
            {
                decoded.Add(new DecodedSection(sectionNumber, section, bytes, null, null, ex.Message));
            }
        }

        // Pass #2
        // recursive descent over the whole image
        var reachableVas = RunRecursiveDescent(decoded);

        // Pass #3
        // filter each section by reachability
        var filtered = new List<DecodedSection>();
        var totalRaw = 0;
        var totalKept = 0;
        foreach (var ds in decoded)
        {
            if (ds.Instructions is null)
            {
                filtered.Add(ds);
                continue;
            }

            var sectionVa = _info.ImageBase + ds.Section.VirtualAddress;
            var kept = ds.Instructions
                .Where(i => reachableVas.Contains(sectionVa + (uint)i.Offset))
                .OrderBy(i => i.Offset)
                .ToArray();

            totalRaw += ds.Instructions.Count;
            totalKept += kept.Length;
            filtered.Add(ds with { Instructions = kept });
        }

        _results.Add($"; Recursive descent: {totalKept} reachable / {totalRaw} decoded instructions");
        _results.Add("; Unreachable bytes are emitted as data below.");

        // Pass #4
        // global label map from "defined as reachable" code
        BuildLabelMap(filtered);

        // Finalize the results
        // Render all program pseudocode
        foreach (var ds in filtered)
        {
            if (ds.Instructions is null)
            {
                _results.Add(";");
                _results.Add(
                    $"; Section#{ds.Number} ({SectionName(ds.Section)}): i386 decoding failed ({ds.Error}), kept as data");
                _results.Add(";");
                var sb = new StringBuilder();
                RenderDataRange(sb, ds.Number, ds.Bytes, 0, ds.Bytes.Length);
                _results.Add(sb.ToString());
                if (DumpStrings) DumpSectionStrings(ds.Number, ds.Bytes);
                continue;
            }

            var analysed = AnalyseInstructions(ds.Number, ds.Instructions, ds.EntryPoints!, ProcessorMode.I386);
            RenderSection(ds.Number, ds.Section, ds.Bytes, analysed, ds.EntryPoints!);
            if (DumpStrings) DumpSectionStrings(ds.Number, ds.Bytes);
        }

        return _results.ToArray();
    }

    /// <summary>
    ///     Builds the set of reachable VAs by following control flow from entry points
    ///     (EP, exports, and whatever the bridge itself discovered).
    ///     Calls are followed too: every call target becomes a function entry.
    /// </summary>
    private HashSet<uint> RunRecursiveDescent(IReadOnlyList<DecodedSection> sections)
    {
        // VA -> (instruction, section VA)
        var map = new Dictionary<uint, (DecodedInstruction Instr, uint SectionVa)>();
        foreach (var ds in sections)
        {
            if (ds.Instructions is null) continue;
            var sectionVa = _info.ImageBase + ds.Section.VirtualAddress;
            foreach (var instr in ds.Instructions)
                map.TryAdd(sectionVa + (uint)instr.Offset, (instr, sectionVa));
        }

        var reachable = new HashSet<uint>();
        var worklist = new Stack<uint>();

        // Seeds
        foreach (var ds in sections)
        {
            if (ds.EntryPoints is null) continue;
            var sectionVa = _info.ImageBase + ds.Section.VirtualAddress;
            foreach (var offset in ds.EntryPoints)
                worklist.Push(sectionVa + (uint)offset);
        }

        while (worklist.Count > 0)
        {
            var va = worklist.Pop();
            if (!reachable.Add(va)) continue;
            if (!map.TryGetValue(va, out var entry)) continue;

            var instr = entry.Instr;
            var sectionVa = entry.SectionVa;

            if (instr.Flow.IsReturn || instr.Flow.IsTerminate || instr.Flow.IsHalt)
                continue;

            // Branch / call targets
            foreach (var target in instr.Targets)
            {
                if (target < 0) continue;
                var targetVa = sectionVa + (uint)target;
                if (IsInsideImage(targetVa))
                    worklist.Push(targetVa);
            }

            // Fall-through (except unconditional branches)
            if (!IsUnconditionalBranch(instr))
            {
                var fallVa = sectionVa + (uint)(instr.Offset + instr.Length);
                if (IsInsideImage(fallVa))
                    worklist.Push(fallVa);
            }
        }

        return reachable;
    }

    private static bool IsUnconditionalBranch(DecodedInstruction instr)
    {
        return instr.Flow.IsJump || instr.Flow.IsIndirectJump;
    }

    private static bool IsBranchOrCall(DecodedInstruction instr)
    {
        return instr.Flow.IsCall || 
               instr.Flow.IsIndirectJumpFar ||
               instr.Flow.IsConditionalJump ||
               instr.Flow.IsIndirectCall || 
               instr.Flow.IsIndirectCallFar;
    }

    /// <summary>
    ///     Builds <see cref="_labelByVa" /> from reachable code only!
    ///     entry points, exports, call targets, other branch targets
    /// </summary>
    private void BuildLabelMap(IReadOnlyList<DecodedSection> sections)
    {
        _labelByVa.Clear();

        if (_entryPointRva != 0)
        {
            var entryVa = _info.ImageBase + _entryPointRva;
            if (IsInsideImage(entryVa))
                _labelByVa[entryVa] = "main"; // <-- Name of the program entry point
        }

        foreach (var exp in _dumpExports)
        {
            var expVa = _info.ImageBase + (uint)exp.Address;
            if (IsInsideImage(expVa) && !_labelByVa.ContainsKey(expVa))
                _labelByVa[expVa] = exp.Name;
        }

        foreach (var ds in sections)
        {
            if (ds.Instructions is null) continue;
            var sectionVa = _info.ImageBase + ds.Section.VirtualAddress;
            foreach (var instr in ds.Instructions)
            {
                var isCall = instr.Flow.Equals(FlowControl.Call);
                foreach (var t in instr.Targets)
                {
                    if (t < 0) continue;
                    var targetVa = sectionVa + (uint)t;
                    if (!IsInsideImage(targetVa)) continue;
                    if (_labelByVa.ContainsKey(targetVa)) continue;

                    _labelByVa[targetVa] = isCall
                        ? $"{ProcedurePrefix}_0x{targetVa:X8}"
                        : $"{LabelPrefix}_0x{targetVa:X8}";
                }
            }
        }
    }

    private static bool IsExecutable(PeSection section)
    {
        return (section.Characteristics & ExecutableMask) != 0;
    }

    private (int section, int offset)? SectionOffset(uint rva)
    {
        var index = 0;
        foreach (var section in _info.Sections.OrderBy(s => s.VirtualAddress))
        {
            index++;
            var spanSize = section.VirtualSize > section.SizeOfRawData
                ? section.VirtualSize
                : section.SizeOfRawData;
            if (spanSize == 0) continue;
            var start = (ulong)section.VirtualAddress;
            var end = start + spanSize;
            var rv = (ulong)rva;
            if (rv >= start && rv < end)
                return (index, (int)(rv - start));
        }

        return null;
    }

    private byte[]? ReadSectionBytes(PeSection section)
    {
        if (section.SizeOfRawData == 0 || section.PointerToRawData == 0)
            return null;

        using var fs = File.OpenRead(_filePath);
        var available = fs.Length - section.PointerToRawData;
        if (available <= 0) return null;

        var spanSize = Math.Max(section.VirtualSize, section.SizeOfRawData);
        var size = (int)Math.Min(spanSize, Math.Min(section.SizeOfRawData, (uint)available));

        using var reader = new BinaryReader(fs);
        reader.BaseStream.Seek(section.PointerToRawData, SeekOrigin.Begin);
        return reader.ReadBytes(size);
    }

    private SortedSet<int> CollectEntryPoints(int sectionNumber, byte[] bytes)
    {
        var entryPoints = new SortedSet<int>();
        var ep = SectionOffset(_entryPointRva);
        if (ep is not null && ep.Value.section == sectionNumber)
            entryPoints.Add(ep.Value.offset);

        foreach (var exp in _dumpExports)
        {
            var eo = SectionOffset((uint)exp.Address);
            if (eo is not null && eo.Value.section == sectionNumber
                               && eo.Value.offset >= 0 && eo.Value.offset < bytes.Length)
                entryPoints.Add(eo.Value.offset);
        }

        return entryPoints;
    }

    private (IReadOnlyList<DecodedInstruction> instructions, SortedSet<int> entrySet, int followed)
        RunAnalysisLoop(byte[] bytes, SortedSet<int> entryPoints)
    {
        var (instructions, _, entries) = PeDecoderBridge.Decode32(bytes, [.. entryPoints]);
        return (instructions, entries, 0);
    }

    private IReadOnlyList<AnalysedInstruction> AnalyseInstructions(
        int sectionNumber,
        IReadOnlyList<DecodedInstruction> instructions,
        SortedSet<int> entrySet,
        ProcessorMode mode)
    {
        var section = _info.Sections.OrderBy(s => s.VirtualAddress).ElementAt(sectionNumber - 1);
        var sectionVaBase = _info.ImageBase + section.VirtualAddress;

        var callTargets = new HashSet<int>(
            instructions
                .Where(i => i.Flow.Equals(FlowControl.Call))
                .SelectMany(i => i.Targets));

        var result = new List<AnalysedInstruction>(instructions.Count);
        foreach (var instruction in instructions)
        {
            var label = ResolveLabel(sectionNumber, instruction.Offset);
            var instrFixups = CollectIatFixupsForInstruction(instruction);
            var analysedOps = new List<AnalysedOperand>(instruction.Operands.Length);
            foreach (var op in instruction.Operands)
            {
                var opFixup = FindIatFixupForOperand(op);
                var text = FormatOperandWithSubstitution(instruction, op, opFixup, mode, sectionVaBase);
                analysedOps.Add(new AnalysedOperand(text, opFixup));
            }

            result.Add(new AnalysedInstruction(
                instruction,
                label,
                entrySet.Contains(instruction.Offset),
                callTargets.Contains(instruction.Offset),
                analysedOps,
                instrFixups));
        }

        return result;
    }

    private string? ResolveLabel(int sectionNumber, int offset)
    {
        var section = _info.Sections.OrderBy(s => s.VirtualAddress).ElementAt(sectionNumber - 1);
        var va = _info.ImageBase + section.VirtualAddress + (uint)offset;
        return _labelByVa.GetValueOrDefault(va);
    }

    private List<IatFixupSite> CollectIatFixupsForInstruction(DecodedInstruction instr)
    {
        var result = new List<IatFixupSite>();
        foreach (var op in instr.Operands)
        {
            var f = FindIatFixupForOperand(op);
            if (f is not null) result.Add(f);
        }

        return result;
    }

    private IatFixupSite? FindIatFixupForOperand(DecodedOperand op)
    {
        var value = OperandValue(op);
        if (value is null) return null;

        if (_iatSymbolByVa.TryGetValue(value.Value, out var symbol))
            return new IatFixupSite(value.Value, symbol);

        if (_iatSymbolByRva.TryGetValue(value.Value, out symbol))
            return new IatFixupSite(value.Value, symbol);

        return null;
    }

    private static uint? OperandValue(DecodedOperand op)
    {
        return op.Part switch
        {
            OperandPart.Imm imm => ToUInt32(imm.Item1),
            OperandPart.SignedImm si => ToUInt32(si.Item1),
            OperandPart.Abs abs => ToUInt32(abs.Item1),
            OperandPart.Mem mem => MemAddress(mem.Item),
            _ => null
        };
    }

    private static uint? ToUInt32(ulong value)
    {
        return value <= uint.MaxValue ? (uint?)value : null;
    }

    private static uint? ToUInt32(long value)
    {
        return value is >= 0 and <= uint.MaxValue ? (uint?)value : null;
    }

    private static uint? MemAddress(string memText)
    {
        if (string.IsNullOrEmpty(memText)) return null;
        var m = Regex.Match(memText, @"0x([0-9A-Fa-f]+)");
        if (m.Success && uint.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, null, out var v)) return v;
        m = Regex.Match(memText, @"\b(\d{6,})\b");
        if (m.Success && uint.TryParse(m.Groups[1].Value, out var v2)) return v2;
        return null;
    }

    private string FormatOperandWithSubstitution(
        DecodedInstruction instr,
        DecodedOperand op,
        IatFixupSite? fixup,
        ProcessorMode mode,
        uint sectionVaBase)
    {
        if (fixup is not null)
            return op.Part switch
            {
                OperandPart.Far => fixup.Symbol,
                OperandPart.Imm => fixup.Symbol,
                OperandPart.SignedImm => fixup.Symbol,
                OperandPart.Abs => $"[{fixup.Symbol}]",
                OperandPart.Mem mem => ReplaceMemDisplacement(mem.Item, fixup.Symbol),
                _ => FormatOperandPlain(instr, op, mode)
            };

        if (op.Part is OperandPart.Mem memOp)
        {
            var addr = MemAddress(memOp.Item);
            if (addr is not null)
            {
                var cell = ResolveMemoryCell(addr.Value);
                if (cell is not null)
                    return ReplaceMemDisplacement(memOp.Item, cell);
            }
        }

        if (op.Part is OperandPart.Imm or OperandPart.SignedImm)
        {
            var val = OperandValue(op);
            if (val is not null)
            {
                // absolute VA
                var resolved = LabelForVa(val.Value);
                if (resolved is not null) return resolved;

                // branch/call target in the decoder's section-relative space
                //       CALL 0x34DC2 resolves the VA [0x401000 + 0x34DC2 = 0x435DC2]
                if (IsBranchOrCall(instr))
                {
                    var asVa = sectionVaBase + val.Value;
                    resolved = LabelForVa(asVa);
                    if (resolved is not null) return resolved;
                }
            }
        }

        if (op.Part is OperandPart.Far far)
        {
            var targetOff = unchecked((int)far.Item2);
            var exports = _exportAt.Where(x => x.Key.off == targetOff).ToArray();
            if (exports.Length == 1) return exports[0].Value;
        }

        return FormatOperandPlain(instr, op, mode);
    }

    private string? LabelForVa(uint va)
    {
        if (_labelByVa.TryGetValue(va, out var lbl)) return lbl;
        if (_stringAt.TryGetValue(va, out _)) return $"str_{va:X8}";

        if (!IsInsideImage(va)) return null;

        return $"{LabelPrefix}_{va:X8}";
    }

    private static string FormatOperandPlain(DecodedInstruction instr, DecodedOperand op, ProcessorMode mode)
    {
        return PeDecoderBridge.FormatOperandPart(mode, instr.Offset, op);
    }

    private static string ReplaceMemDisplacement(string memText, string symbol)
    {
        var plus = memText.LastIndexOf('+');
        var minus = memText.LastIndexOf('-');
        var cut = Math.Max(plus, minus);
        if (cut <= 0) return $"[{symbol}]";
        return memText[..cut] + symbol + memText[cut..];
    }

    private void RenderSection(int sectionNumber, PeSection section, byte[] bytes,
        IReadOnlyList<AnalysedInstruction> analysed, SortedSet<int> entrySet)
    {
        var sb = new StringBuilder();
        var labelCount = analysed.Count(a => a.Label is not null);

        sb.AppendLine(";");
        sb.AppendLine($";         Section#{sectionNumber} ({SectionName(section)})");
        sb.AppendLine("; Mode:        32-bit");
        sb.AppendLine($"; Reachable:   {analysed.Count} instructions / {labelCount} labels");
        sb.AppendLine(";");

        var cover = 0;
        foreach (var analysedInstruction in analysed)
        {
            var instr = analysedInstruction.Instr;
            if (instr.Offset < cover) continue;

            // "Paddings" between previous reachable block and this one will be marked raw data.
            // Think coffeelake, think again
            if (instr.Offset > cover)
                RenderDataRange(sb, sectionNumber, bytes, cover, instr.Offset - cover);

            if (analysedInstruction.Label is not null)
                sb.AppendLine($"{analysedInstruction.Label}:");

            var operandText = string.Join(", ", analysedInstruction.Operands
                .Select(o => o.Text).Where(t => t.Length > 0));
            var fullText = operandText.Length > 0
                ? $"{instr.Mnemonic} {operandText}"
                : instr.Mnemonic;
            var bytesStr = BitConverter.ToString(instr.Bytes).Replace("-", " ");

            var xrefComment = analysedInstruction.AllFixups.Count > 0
                ? " xref: " + string.Join(", ", analysedInstruction.AllFixups.Select(f => f.Symbol))
                : string.Empty;
            var suffixComment = instr.Comment is not null && instr.Comment.Value.Length > 0
                ? instr.Comment.Value
                : string.Empty;

            sb.AppendLine(
                $"\t{fullText,-30} ; {sectionNumber}:0x{instr.Offset:X4} {bytesStr}{suffixComment}{xrefComment}");

            if (instr.Flow.IsReturn)
                sb.AppendLine($"; {analysedInstruction.Label} returned a flow control");
            else if (instr.Flow.IsTerminate)
                sb.AppendLine($"Terminated ({suffixComment})");
            else if (instr.Flow.IsHalt)
                sb.AppendLine("; System Halted");

            cover = instr.Offset + instr.Length;
        }

        if (cover < bytes.Length)
            RenderDataRange(sb, sectionNumber, bytes, cover, bytes.Length - cover);

        _results.Add(sb.ToString());
    }

    private static string SectionName(PeSection section)
    {
        return new string(section.Name).TrimEnd('\0');
    }

    private void DumpSectionStrings(int sectionNumber, byte[] bytes)
    {
        if (!DumpStrings) return;
        var strings = CollectAsciiStrings(bytes, MinStringLength);
        if (strings.Count == 0) return;

        _results.Add(string.Empty);
        _results.Add($"; --- ASCII strings of Section#{sectionNumber} ({strings.Count}) ---");
        foreach (var (offset, text) in strings)
        {
            var safe = text.Replace("\"", "\\\"");
            _results.Add($"\tDB \"{safe}\",".PadRight(42) +
                         $" ; {sectionNumber}:0x{offset:X4} len={text.Length} bytes");
        }
    }

    private static List<(int offset, string text)> CollectAsciiStrings(byte[] bytes, int minLength)
    {
        var result = new List<(int offset, string text)>();
        var i = 0;
        while (i < bytes.Length)
            if (bytes[i] >= 32 && bytes[i] < 127)
            {
                var start = i;
                while (i < bytes.Length && bytes[i] >= 32 && bytes[i] < 127) i++;
                if (i - start >= minLength)
                    result.Add((start, Encoding.ASCII.GetString(bytes, start, i - start)));
            }
            else
            {
                i++;
            }

        return result;
    }

    private static void RenderDataRange(StringBuilder sb, int sectionNumber, byte[] bytes, int offset, int length)
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
                              $" ; {sectionNumber}:0x{runStart:X4} padding");
                continue;
            }

            var pos = runStart;
            while (pos < mutableI && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, mutableI - pos);
                var payload = string.Join(", ", bytes[pos..(pos + take)].Select(b => $"0x{b:X2}"));
                sb.AppendLine($"\tDB {payload},".PadRight(30) + $" ; {sectionNumber}:0x{pos:X4} data");
                pos += take;
                linesLeft--;
            }
        }

        if (mutableI < limit)
            sb.AppendLine($"; {sectionNumber}:0x{mutableI:X4} {limit - mutableI} more byte(s) of data (omitted)");
    }

    private sealed class IatFixupSite(uint rva, string symbol)
    {
        public readonly uint Rva = rva;
        public readonly string Symbol = symbol;
    }

    private sealed record AnalysedOperand(string Text, IatFixupSite? Fixup);

    private sealed record AnalysedInstruction(
        DecodedInstruction Instr,
        string? Label,
        bool IsEntryPoint,
        bool IsCallTarget,
        IReadOnlyList<AnalysedOperand> Operands,
        IReadOnlyList<IatFixupSite> AllFixups);

    /// <summary>
    ///     Intermediate review of pass #1 (see Disassemble subroutine)
    /// </summary>
    private sealed record DecodedSection(
        int Number,
        PeSection Section,
        byte[] Bytes,
        IReadOnlyList<DecodedInstruction>? Instructions,
        SortedSet<int>? EntryPoints,
        string? Error = null);
}