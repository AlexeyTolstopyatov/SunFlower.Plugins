// CoffeeLake (C) 2026-*
//
// ELF decoder: recursive-descent disassembler over the Intel bridge.
//   - symbol substitution from .symtab / .dynsym
//   - GOT/PLT import resolution via .rela.dyn / .rela.plt
//   - dynamic seeds from DT_INIT / DT_INIT_ARRAY / etc.
//   - sub_XX_YYYY / loc_XX_YYYY for local branch/call targets
//
// Supports ELF32 and ELF64, LE and BE.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Sunflower.Dasm.Intel.Core;
using SunFlower.Elf.Headers;
using SunFlower.Elf.Models;

namespace SunFlower.Elf.Services;

internal sealed record DecodedSection(
    int Number,
    ElfSection Section,
    byte[] Bytes,
    IReadOnlyList<DecodedInstruction>? Instructions,
    SortedSet<int>? EntryPoints,
    string? Error = null);

public sealed class ElfDecoderService
{
    // Standard ELF section types (may vary)
    private const int SymTab = 2;
    private const int RelAddend   = 4;
    private const int Rel    = 9;
    private const int Dynamic = 11;

    // Standard ELF dynamic tags
    private const long Init           = 12;
    private const long Fini           = 13;
    private const long InitArray     = 25;
    private const long FiniArray     = 26;
    private const long PreinitArray  = 32;

    // x86/x64 relocation types that write GOT slots
    private const int I386GlobalData  = 6;
    private const int I386JumpSlot  = 7;
    private const int I386Relative  = 8;
    
    private const int Amd64GlobalStart = 6;
    private const int Amd64JumpSlot = 7;
    private const int Amd64Relative = 8;
    
    private readonly string _filePath;
    private readonly ElfDumpManager _dump;
    private readonly ElfImageDetails _info;
    private readonly bool _is64Bit;

    private readonly Dictionary<(int section, int off), string> _symbolAt = [];
    private readonly Dictionary<(int section, int off), string> _localLabelAt = [];
    private readonly Dictionary<ulong, string> _gotToImport = [];
    private readonly Dictionary<ulong, string> _symbolByVa = [];
    
    private readonly List<string> _results = [];

    public bool DumpStrings { get; set; } = true;
    public int MinStringLength { get; set; } = 4;

    public ElfDecoderService(string filePath, ElfDumpManager dump)
    {
        _filePath = filePath;
        _dump = dump;
        _info = dump.ImageDetails;
        _is64Bit = dump.Is64Bit;

        BuildSymbolMap();
        BuildImportMap();
    }

    public string[] Disassemble()
    {
        _results.Clear();

        _results.Add(";");
        _results.Add($";        This file is **generated** at {DateTime.UtcNow:u}");
        _results.Add("; Sunflower.Elf.dll proceed the original image and emit this file; CoffeeLake (C) 2026-*");
        _results.Add($"; Class:     {(_is64Bit ? "ELF64" : "ELF32")}");
        _results.Add($"; Machine:   {_info.Machine}");
        _results.Add($"; Type:      {_info.Type}");
        _results.Add($"; Entry:     0x{_info.EntryPoint:X}");
        _results.Add($"; Sections:  {_dump.Sections.Length}");
        _results.Add($"; Symbols:   {_dump.Symbols.Length}");
        _results.Add($"; Dynamic:   {_dump.DynamicEntries.Length}");
        _results.Add($"; Imports:   {_gotToImport.Count}");
        _results.Add(";");

        var dyns = _dump.DynamicEntries;
        var initFns = dyns.Count(d => (long)d.Tag is Init or Fini);
        var initArrays = dyns.Count(d => (long)d.Tag is InitArray or FiniArray or PreinitArray);
        
        if (initFns + initArrays > 0)
            _results.Add($"; Dynamic seeds: {initFns} single + {initArrays} array(s)");

        // Executable sections sorted by load address
        var execSections = _info.Sections
            .Select((s, idx) => (Section: s, Index: idx))
            .Where(t => t.Section.IsExecutable)
            .OrderBy(t => t.Section.Address)
            .ToArray();

        if (execSections.Length == 0)
        {
            _results.Add(";");
            _results.Add("; No executable sections found.");
            return _results.ToArray();
        }

        foreach (var (section, index) in execSections)
        {
            var bytes = ReadSectionBytes(section);
            if (bytes.Length == 0)
            {
                _results.Add(";");
                _results.Add($"; Section #{index} ({section.Name}) is empty, skipped.");
                continue;
            }

            ProcessSection(new DecodedSection(index, section, bytes, null, null));
        }

        return _results.ToArray();
    }
    
    private void ProcessSection(DecodedSection ds)
    {
        var seeds = CollectSeeds(ds.Number, ds.Section, ds.Bytes);

        // Fallback for small executable sections without symbols (.init, .fini, .plt)
        if (seeds.Count == 0 && ds.Bytes.Length <= 0x1000)
            seeds.Add(0);

        if (seeds.Count == 0)
        {
            _results.Add(";");
            _results.Add($"; Section #{ds.Number} ({ds.Section.Name}): no seeds found, kept as data.");
            _results.Add(";");
            
            _results.Add($"SECTION {ds.Section.Name}");
            var sbEmpty = new StringBuilder();
            
            RenderDataRange(sbEmpty, ds.Number, ds.Bytes, 0, ds.Bytes.Length);
            _results.Add(sbEmpty.ToString());
            
            DumpSectionStrings(ds.Number, ds.Bytes);
            
            _results.Add("ENDS");
            return;
        }

        IReadOnlyList<DecodedInstruction> instructions;
        SortedSet<int> bridgeEntries;
        try
        {
            (instructions, bridgeEntries) = Decode(ds.Bytes, seeds);
        }
        catch (Exception ex)
        {
            _results.Add($"SECTION {ds.Section.Name}");
            _results.Add(";");
            _results.Add($"; Section #{ds.Number} wasn't completely retranslated.");
            _results.Add($"; Reason: {ex.Message}");
            _results.Add(";");

            var sbError = new StringBuilder();
            
            RenderDataRange(sbError, ds.Number, ds.Bytes, 0, ds.Bytes.Length);
            _results.Add(sbError.ToString());
            DumpSectionStrings(ds.Number, ds.Bytes);
            
            _results.Add("ENDS");
            return;
        }

        var allSeeds = new SortedSet<int>(seeds);
        allSeeds.UnionWith(bridgeEntries);
        
        var reachable = RunRecursiveDescent(instructions, allSeeds);
        var kept = instructions
            .Where(i => reachable.Contains(i.Offset))
            .OrderBy(i => i.Offset)
            .ToArray();

        BuildLocalLabels(ds.Number, kept);

        var filled = ds with
        {
            Instructions = instructions, 
            EntryPoints = bridgeEntries
        };
        RenderSection(filled, kept);
        DumpSectionStrings(ds.Number, ds.Bytes);
    }
    
    private void BuildSymbolMap()
    {
        foreach (var sym in _dump.Symbols)
        {
            if (string.IsNullOrEmpty(sym.Name)) 
                continue;
            
            if (sym.SectionIndex == 0 || sym.SectionIndex >= _dump.Sections.Length) 
                continue;

            var section = _dump.Sections[sym.SectionIndex];
            
            if (!section.IsAllocated) 
                continue;
            
            if (sym.Value < section.Address) 
                continue;

            var off = sym.Value - section.Address;
            if (off >= section.Size) 
                continue;

            _symbolAt[(sym.SectionIndex, (int)off)] = sym.Name;
            _symbolByVa[sym.Value] = sym.Name;
        }
    }

    private void BuildImportMap()
    {
        // .dynsym and its string table are required to name relocations
        var dynsym = FindSectionByType(Dynamic);
        if (dynsym is null) return;

        var dynsymBytes = ReadSectionBytes(dynsym);
        var dynstr = ReadSectionByIndex(dynsym.Link);
        var dynSyms = ParseDynamicSymbols(dynsymBytes, dynstr);
        if (dynSyms.Count == 0) return;

        // Register .dynsym
        for (var i = 0; i < dynSyms.Count; i++)
        {
            var sym = dynSyms[i];
            if (string.IsNullOrEmpty(sym.Name)) 
                continue;
            if (sym.SectionIndex == 0 || sym.SectionIndex >= _dump.Sections.Length) 
                continue;
            
            var sec = _dump.Sections[sym.SectionIndex];
            if (!sec.IsAllocated) 
                continue;
            if (sym.Value < sec.Address) 
                continue;
            
            var off = sym.Value - sec.Address;
            if (off >= sec.Size) 
                continue;

            var key = ((int)sym.SectionIndex, (int)off);
            if (!_symbolAt.ContainsKey(key))
                _symbolAt[key] = sym.Name;
            if (!_symbolByVa.ContainsKey(sym.Value))
                _symbolByVa[sym.Value] = sym.Name;
        }

        // relocation tables (.rela.* / .rel.*)
        foreach (var section in _dump.Sections)
        {
            if ((int)section.Type != Rel && (int)section.Type != RelAddend) continue;

            var relaBytes = ReadSectionBytes(section);
            var isRela = (int)section.Type == RelAddend;
            var entrySize = isRela
                ? _is64Bit ? 24 : 12
                : _is64Bit ? 16 : 8;
            var count = relaBytes.Length / entrySize;

            for (var i = 0; i < count; i++)
            {
                var at = i * entrySize;
                ulong rOffset;
                ulong rInfo;
                
                long addend = 0;

                if (_is64Bit)
                {
                    rOffset = ReadU64(relaBytes, at);
                    rInfo = ReadU64(relaBytes, at + 8);
                    if (isRela) 
                        addend = (long)ReadU64(relaBytes, at + 16);
                }
                else
                {
                    rOffset = ReadU32(relaBytes, at);
                    rInfo = ReadU32(relaBytes, at + 4);
                    if (isRela) 
                        addend = (int)ReadU32(relaBytes, at + 8);
                }

                var relType = _is64Bit ? (int)(rInfo & 0xFFFFFFFF) : (int)(rInfo & 0xFF);
                var symIndex = _is64Bit ? (int)(rInfo >> 32) : (int)(rInfo >> 8);

                if (symIndex == 0 || symIndex >= dynSyms.Count) continue;

                var sym = dynSyms[symIndex];
                if (string.IsNullOrEmpty(sym.Name)) continue;

                // GLOB_DAT / JUMP_SLOT write an imported symbol address into a GOT slot
                var isImportSlot = _is64Bit
                    ? relType is Amd64GlobalStart or Amd64JumpSlot
                    : relType is I386GlobalData or I386JumpSlot;

                if (isImportSlot)
                {
                    _gotToImport[rOffset] = sym.Name;
                }
                // RELATIVES
                // rOffset is a pointer slot to addend (local target);
                // useful only for data refs, skip for code.
            }
        }
    }

    private ElfSection? FindSectionByType(int type)
    {
        foreach (var s in _dump.Sections)
            if ((int)s.Type == type) return s;
        return null;
    }

    private byte[] ReadSectionByIndex(uint index)
    {
        if (index == 0 || index >= _dump.Sections.Length) return [];
        return ReadSectionBytes(_dump.Sections[index]);
    }

    private sealed record DynSymbol(string Name, ulong Value, ushort SectionIndex);

    private List<DynSymbol> ParseDynamicSymbols(byte[] symBytes, byte[] strBytes)
    {
        var result = new List<DynSymbol>();
        var entrySize = _is64Bit ? 24 : 16;
        var count = symBytes.Length / entrySize;

        for (var i = 0; i < count; i++)
        {
            var at = i * entrySize;
            uint nameOffset;
            ulong value;
            ushort shndx;

            if (_is64Bit)
            {
                nameOffset = ReadU32(symBytes, at);
                // st_info at +4, st_other at +5
                shndx = ReadU16(symBytes, at + 6);
                value = ReadU64(symBytes, at + 8);
            }
            else
            {
                nameOffset = ReadU32(symBytes, at);
                value = ReadU32(symBytes, at + 4);
                // st_size at +8, st_info at +12, st_other at +13
                shndx = ReadU16(symBytes, at + 14);
            }

            result.Add(new DynSymbol(GetString(strBytes, nameOffset), value, shndx));
        }
        return result;
    }

    private SortedSet<int> CollectSeeds(int sectionIndex, ElfSection section, byte[] bytes)
    {
        var seeds = new SortedSet<int>();

        AddSeed(_info.EntryPoint, section, bytes, seeds);

        // FUNC symbols in this section
        foreach (var sym in _dump.Symbols)
        {
            if (sym.Type != ElfSymbolType.Func) 
                continue;
            if (sym.SectionIndex != sectionIndex) 
                continue;
            
            AddSeed(sym.Value, section, bytes, seeds);
        }

        // Dynamic entry function pointers
        foreach (var dyn in _dump.DynamicEntries)
        {
            switch ((long)dyn.Tag)
            {
                case Init:
                case Fini:
                    AddSeed(dyn.Val, section, bytes, seeds);
                    break;

                case InitArray:
                case FiniArray:
                case PreinitArray:
                    AddArraySeeds(dyn.Val, section, bytes, seeds);
                    break;
            }
        }

        return seeds;
    }

    private static void AddSeed(ulong va, ElfSection section, byte[] bytes, SortedSet<int> seeds)
    {
        if (va < section.Address) 
            return;
        var delta = va - section.Address;
        if (delta >= (ulong)bytes.Length) 
            return;
        seeds.Add((int)delta);
    }

    private void AddArraySeeds(ulong arrayVa, ElfSection targetSection, byte[] targetBytes, SortedSet<int> seeds)
    {
        // Find which section holds the array itself
        ElfSection? arraySection = null;
        foreach (var s in _dump.Sections)
        {
            if (!s.IsAllocated) continue;
            if (arrayVa >= s.Address && arrayVa < s.Address + s.Size)
            {
                arraySection = s;
                break;
            }
        }
        if (arraySection is null) 
            return;

        var arrayBytes = ReadSectionBytes(arraySection);
        var ptrSize = _is64Bit ? 8 : 4;
        var startOff = (int)(arrayVa - arraySection.Address);

        for (var i = startOff; i + ptrSize <= arrayBytes.Length; i += ptrSize)
        {
            var ptr = _is64Bit
                ? ReadU64(arrayBytes, i)
                : ReadU32(arrayBytes, i);
            if (ptr == 0) break;
            AddSeed(ptr, targetSection, targetBytes, seeds);
        }
    }
    
    private (IReadOnlyList<DecodedInstruction>, SortedSet<int>) Decode(byte[] bytes, SortedSet<int> seeds)
    {
        var (instrs, _, entries) = ElfDecoderBridge.Decode(bytes, [.. seeds], _is64Bit);
        return (instrs, entries);
    }

    private static HashSet<int> RunRecursiveDescent(
        IReadOnlyList<DecodedInstruction> instructions,
        IEnumerable<int> seeds)
    {
        var byOffset = new Dictionary<int, DecodedInstruction>(instructions.Count);
        foreach (var i in instructions) 
            byOffset.TryAdd(i.Offset, i);

        var reachable = new HashSet<int>();
        var worklist = new Stack<int>(seeds);

        while (worklist.Count > 0)
        {
            var off = worklist.Pop();
            if (!reachable.Add(off)) 
                continue;
            
            if (!byOffset.TryGetValue(off, out var instr)) 
                continue;

            if (instr.Flow.IsReturn || instr.Flow.IsTerminate || instr.Flow.IsHalt)
                continue;

            foreach (var t in instr.Targets)
            {
                if (t >= 0 && byOffset.ContainsKey(t))
                    worklist.Push(t);
            }

            if (!IsUnconditionalBranch(instr))
            {
                var next = off + instr.Length;
                if (byOffset.ContainsKey(next))
                    worklist.Push(next);
            }
        }

        return reachable;
    }

    private static bool IsUnconditionalBranch(DecodedInstruction instr)
    {
        var m = instr.Mnemonic.ToUpperInvariant();
        return m is "JMP" or "JMPF" or "JMPE" or "JMP SHORT" or "JMP NEAR";
    }

    private void BuildLocalLabels(int sectionIndex, IReadOnlyList<DecodedInstruction> instructions)
    {
        var keptOffsets = new HashSet<int>(instructions.Select(i => i.Offset));

        foreach (var instr in instructions)
        {
            var isCall = instr.Flow.Equals(FlowControl.Call);
            foreach (var t in instr.Targets)
            {
                if (t < 0 || !keptOffsets.Contains(t)) continue;
                var key = (sectionIndex, t);
                if (_symbolAt.ContainsKey(key)) continue;
                if (_localLabelAt.ContainsKey(key)) continue;
                _localLabelAt[key] = isCall
                    ? $"{ProcedurePrefix}_0x{sectionIndex:X2}_0x{t:X4}"
                    : $"{LabelPrefix}_0x{t:X4}";
            }
        }
    }

    private const string LabelPrefix = "procedure";
    private const string ProcedurePrefix = "label";

    private void RenderSection(DecodedSection ds, IReadOnlyList<DecodedInstruction> instructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SECTION .{ds.Section.Name}");
        
        sb.AppendLine(";");
        sb.AppendLine($"; Type: {ds.Section.Type}, Flags: {ds.Section.Flags}");
        sb.AppendLine($"; Address: 0x{ds.Section.Address:X}, Size: 0x{ds.Section.Size:X}");
        sb.AppendLine($"; Kept: {instructions.Count} / {ds.Instructions!.Count} instructions");
        sb.AppendLine(";");
        
        var cover = 0;
        foreach (var instr in instructions)
        {
            if (instr.Offset > cover)
                RenderDataRange(sb, ds.Number, ds.Bytes, cover, instr.Offset - cover);

            cover = Math.Max(cover, instr.Offset + instr.Length);

            var label = ResolveLabel(ds.Number, instr.Offset);
            
            if (label is not null) 
                sb.AppendLine($"{label}:");

            var operandText = FormatOperands(ds.Number, ds.Section, instr);
            var fullText = string.IsNullOrEmpty(operandText)
                ? instr.Mnemonic
                : $"{instr.Mnemonic} {operandText}";
            var bytesStr = BitConverter.ToString(instr.Bytes).Replace("-", " ");

            sb.AppendLine($"\t{fullText,-40} ; {ds.Number}:0x{instr.Offset:X4} {bytesStr}");
        }

        if (cover < ds.Bytes.Length)
            RenderDataRange(sb, ds.Number, ds.Bytes, cover, ds.Bytes.Length - cover);

        _results.Add(sb.ToString());
    }

    private string? ResolveLabel(int sectionIndex, int offset)
    {
        return _symbolAt.TryGetValue((sectionIndex, offset), out var sym) 
            ? sym 
            : _localLabelAt.GetValueOrDefault((sectionIndex, offset));
    }

    private string FormatOperands(int sectionIndex, ElfSection section, DecodedInstruction instr)
    {
        if (instr.Operands.Length == 0) return string.Empty;
        var parts = new List<string>(instr.Operands.Length);
        
        parts.AddRange(instr.Operands.Select(op => FormatOperand(sectionIndex, section, instr, op)));
        
        return string.Join(", ", parts);
    }

    private string FormatOperand(int sectionIndex, ElfSection section, DecodedInstruction instr, DecodedOperand op)
    {
        // Branch / call target
        if (instr.Targets.Count() == 1 && op.Part is OperandPart.Imm or OperandPart.SignedImm)
        {
            var target = instr.Targets[0];
            if (target >= 0)
            {
                var label = ResolveLabel(sectionIndex, target);
                if (label is not null) return label;

                // Cross-section: target offset maps to another section's VA
                var targetVa = section.Address + (ulong)target;
                if (_symbolByVa.TryGetValue(targetVa, out var crossSym))
                    return crossSym;
            }
        }

        // Memory operand
        if (op.Part is OperandPart.Mem mem)
        {
            var va = ResolveMemoryVa(section, instr, mem.Item);
            if (va is not null)
            {
                if (_gotToImport.TryGetValue(va.Value, out var imp))
                    return $"[{imp}]";

                if (_symbolByVa.TryGetValue(va.Value, out var sym))
                    return $"[{sym}]";
            }
        }

        // Immediate (32-bit PIC GOT slot)
        if (!_is64Bit && op.Part is OperandPart.Imm imm)
        {
            var va = imm.Item1;
            if (_gotToImport.TryGetValue(va, out var imp)) return imp;
            if (_symbolByVa.TryGetValue(va, out var sym)) return sym;
        }

        return ElfDecoderBridge.FormatOperandPart(ProcessorMode.I386, instr.Offset, op);
    }

    /// <summary> Parse memory operand text and compute effective VA when possible. </summary>
    private static ulong? ResolveMemoryVa(ElfSection section, DecodedInstruction instr, string memText)
    {
        if (string.IsNullOrEmpty(memText)) return null;

        // RIP-relative x86-64
        // [rip+0x...] or [rip-0x...] (or with extra base/index - give up)
        var idx = memText.IndexOf("rip", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = memText[(idx + 3)..];
            var plus = rest.IndexOf('+');
            var minus = rest.IndexOf('-');
            var sign = plus >= 0 ? 1 : minus >= 0 ? -1 : 0;
            if (sign == 0) 
                return null;
            var cut = sign > 0 ? plus : minus;
            var dispStr = rest[(cut + 1)..].TrimEnd(']', ' ');
            if (!ulong.TryParse(dispStr.Replace("0x", ""),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var disp))
                return null;

            var rip = section.Address + (ulong)(instr.Offset + instr.Length);
            return sign > 0 ? rip + disp : rip - disp;
        }

        // Absolute
        // [0x...]
        if (memText.StartsWith("[0x", StringComparison.OrdinalIgnoreCase))
        {
            var end = memText.IndexOf(']');
            if (end < 0) return null;
            var hex = memText[3..end];
            if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var va))
                return va;
        }

        return null;
    }
    
    private void DumpSectionStrings(int sectionIndex, byte[] bytes)
    {
        if (!DumpStrings) return;
        var strings = CollectAsciiStrings(bytes, MinStringLength);
        if (strings.Count == 0) return;

        _results.Add(string.Empty);
        _results.Add($"; --- ASCII strings of Section#{sectionIndex} ({strings.Count}) ---");
        foreach (var (off, text) in strings)
        {
            var safe = text.Replace("\"", "\\\"");
            _results.Add($"\tDB \"{safe}\",".PadRight(42) + $" ; {sectionIndex}:0x{off:X4} len={text.Length}");
        }
    }

    private static List<(int offset, string text)> CollectAsciiStrings(byte[] bytes, int minLength)
    {
        var result = new List<(int, string)>();
        var i = 0;
        while (i < bytes.Length)
        {
            if (bytes[i] >= 32 && bytes[i] < 127)
            {
                var start = i;
                while (i < bytes.Length && bytes[i] >= 32 && bytes[i] < 127) i++;
                if (i - start >= minLength)
                    result.Add((start, Encoding.ASCII.GetString(bytes, start, i - start)));
            }
            else i++;
        }
        return result;
    }

    private static void RenderDataRange(StringBuilder sb, int sectionNumber, byte[] bytes, int offset, int length)
    {
        const int zeroThreshold = 4;
        const int bytesPerLine = 16;
        const int maxLines = 16;

        var pos = offset;
        var limit = offset + length;
        var linesLeft = maxLines;

        while (pos < limit && linesLeft > 0)
        {
            var runStart = pos;
            var runByte = bytes[pos];
            while (pos < limit && bytes[pos] == runByte) pos++;
            var runLen = pos - runStart;

            if (runByte == 0 && runLen >= zeroThreshold)
            {
                sb.AppendLine($"\tDB {runLen} DUP (0x00),".PadRight(30) + $" ; {sectionNumber}:0x{runStart:X4} padding");
                continue;
            }

            var p = runStart;
            while (p < pos && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, pos - p);
                var payload = string.Join(", ", bytes[p..(p + take)].Select(b => $"0x{b:X2}"));
                sb.AppendLine($"\tDB {payload},".PadRight(30) + $" ; {sectionNumber}:0x{p:X4} data");
                p += take;
                linesLeft--;
            }
        }

        if (pos < limit)
            sb.AppendLine($"; {sectionNumber}:0x{pos:X4} {limit - pos} more byte(s) omitted");
    }

    private byte[] ReadSectionBytes(ElfSection section)
    {
        if (section.Size == 0) return [];
        
        using var fs = File.OpenRead(_filePath);
        using var br = new BinaryReader(fs);

        br.BaseStream.Seek((long)section.Offset, SeekOrigin.Begin);
        
        var data = new byte[section.Size];
        var read = br.Read(data, 0, data.Length);
        
        if (read < data.Length) Array.Resize(ref data, read);
        
        return data;
    }

    private ulong ReadU64(byte[] data, int offset) =>
        _info.IsLittleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8))
            : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8));

    private uint ReadU32(byte[] data, int offset) =>
        _info.IsLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private ushort ReadU16(byte[] data, int offset) =>
        _info.IsLittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private static string GetString(byte[] table, uint offset)
    {
        if (offset == 0 || offset >= table.Length) return string.Empty;
        var start = (int)offset;
        var end = start;
        while (end < table.Length && table[end] != 0) end++;
        return Encoding.ASCII.GetString(table, start, end - start);
    }
}