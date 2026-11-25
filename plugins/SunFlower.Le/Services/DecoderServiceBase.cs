// CoffeeLake (C) 2026-*
//
// Shared Linear executable decoder base
//
// All format-agnostic logic lives here:
//   - export map construction from entry bundles
//   - fixup map construction from fixup pages / records
//   - object translation (disassembly -> analysis -> rendering)
//   - operand resolution with fixup substitution (no regular expressions)
//
// Subclasses only implement format-specific parts:
//   - reading object bytes from file pages
//   - extracting properties from the platform-specific file object

using System.Globalization;
using System.Text;
using Sunflower.Dasm.Intel.Core;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public abstract class DecoderServiceBase
{
    /// <summary>
    /// Resolved fixup site. Carries enough info so that far operands can
    /// combine "object from fixup" with "offset from code" (classic
    /// linker behavior where the selector half is a placeholder).
    /// </summary>
    protected sealed class FixupSite(
        int objNum, int objOff, int size, string symbol,
        int targetObjNum, uint targetOffset, uint? additive, bool isSelectorOnly)
    {
        public readonly int ObjNum = objNum;
        public readonly int ObjOff = objOff;
        public readonly int Size = size;
        public readonly string Symbol = symbol;

        /// <summary>Object number referenced by the fixup target (0 = N/A / import).</summary>
        public readonly int TargetObjNum = targetObjNum;

        /// <summary>Raw target offset from the fixup record (0 when selector-only).</summary>
        public readonly uint TargetOffset = targetOffset;

        /// <summary>Additive to add to target offset, if present.</summary>
        public readonly uint? Additive = additive;

        /// <summary>
        /// True when the fixup only rewrites the selector half of a far pointer.
        /// In this case the real target offset is already in the code bytes,
        /// and TargetOffset here is a placeholder (usually 0).
        /// </summary>
        public readonly bool IsSelectorOnly = isSelectorOnly;
    }

    private sealed record AnalysedOperand(string Text, FixupSite? Fixup);

    private sealed record AnalysedInstruction(
        DecodedInstruction Instr,
        string? Label,
        bool IsEntryPoint,
        bool IsCallTarget,
        IReadOnlyList<AnalysedOperand> Operands,
        IReadOnlyList<FixupSite> AllFixups
    );


    /// <summary>
    ///     Single dictionary for procedures and labels/image locations
    /// </summary>
    private readonly   Dictionary<(int obj, int off), string> _localLabelAt = [];
    protected readonly Dictionary<int, SortedSet<int>> FixupTargetsByObject = [];
    protected readonly Dictionary<(int obj, int off), FixupSite> FixupAt = [];
    protected readonly Dictionary<(int obj, int off), string> ExportAt = [];
    protected readonly List<string> Results = [];

    private const string ProcedurePrefix = "procedure";
    private const string LabelPrefix = "label";

    protected readonly int MainObj;
    protected readonly uint MainEip;
    protected readonly int PageSize;
    protected readonly string FilePath;

    protected const int LeMaxAnalysisPasses = 8;

    public bool AnalyseDataObjects { get; set; } = true;
    public bool DumpStrings { get; set; } = true;
    public int MinStringLength { get; set; } = DataObjectExtension.DefaultMinStringLength;
    public bool DiscoverDataProcedures { get; set; } = false;

    protected DecoderServiceBase(string filePath, int mainObj, uint mainEip, int pageSize,
        ImportRecord[] importRecords)
    {
        FilePath = filePath;
        MainObj = mainObj;
        MainEip = mainEip;
        PageSize = pageSize;
        DumpImportRecords = importRecords;
    }

    protected ExportRecord[] DumpResidentNames = [];
    protected ExportRecord[] DumpNonResidentNames = [];
    protected EntryBundle[] DumpEntryBundles = [];
    protected ImportRecord[] DumpImportRecords = [];
    protected uint[] DumpFixupPageOffsets = [];
    protected FixupRecord[] DumpFixupRecords = [];
    protected FileObject[] DumpObjects = [];

    protected abstract uint GetObjectPageMapIndex(FileObject fileObject);
    protected abstract uint GetObjectPageMapEntries(FileObject obj);
    protected abstract uint GetObjectVirtualSegmentSize(FileObject obj);
    protected abstract uint GetObjectObjectFlagsMask(FileObject obj);
    protected abstract bool GetObjectExecute(FileObject obj);
    protected abstract bool GetObjectResource(FileObject obj);
    protected abstract string[] GetObjectFlags(FileObject obj);
    protected abstract string GetObjectSuggestedName(FileObject obj);
    protected abstract byte[]? BuildObjectBytes(FileObject obj);

    public string[] Disassemble()
    {
        var moduleName = DumpResidentNames.Length > 0
            ? DumpResidentNames[0].String
            : "<unknown_module>";

        Results.AddRange([
            ";",
            $";     This file is **generated** at {DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}UTC",
            $"; DOS Interrupt map:     {File.Exists(DecoderBridge.DosProgramInterruptsMapPath)}",
            $"; Runtime Import Fixups: {DumpImportRecords.Length}",
            $"; Entry Points:          {DumpEntryBundles.SelectMany(x => x.Entries).Count()}",
            $"; Total Fixup Records:   {DumpFixupRecords.Length}",
            
            ";",
            ".MODEL FLAT",
            ""
        ]);

        Results.AddRange(ExternalEntryPointsExtension.AddSignatures(
            DumpEntryBundles.SelectMany(x => x.Entries).ToArray()));

        BuildExportMap();
        BuildFixupMap();

        for (var i = 0; i < DumpObjects.Length; i++)
        {
            var obj = DumpObjects[i];
            if (GetObjectVirtualSegmentSize(obj) == 0) continue;
            if (!AnalyseDataObjects && GetObjectResource(obj)) continue;
            TranslateObject(i + 1, obj);
        }

        Results.Add("END");
        return Results.ToArray();
    }

    private void BuildExportMap()
    {
        foreach (var bundle in DumpEntryBundles)
        {
            if (bundle.Count == 0) continue;

            var objNum = bundle.ObjectNumber;

            foreach (var entry in bundle.Entries.Where(entry => entry is not (EntryUnused or EntryForwarder)))
            {
                (int Offset, string EntryName) pair = entry switch
                {
                    Entry16Bit e16 => (e16.Offset, e16.EntryName),
                    Entry32Bit e32 => ((int)e32.Offset, e32.EntryName),
                    Entry286CallGate eg => (eg.Offset, eg.EntryName),
                    _ => (-1, "<unbelievable entry point>")
                };
                var name = string.IsNullOrEmpty(pair.EntryName) ? $"@{entry.Ordinal}" : pair.EntryName;

                if (pair.Offset >= 0 && objNum > 0)
                    ExportAt[(objNum, pair.Offset)] = FailSafe(name);
            }
        }
    }

    private void BuildFixupMap()
    {
        if (DumpFixupPageOffsets.Length < 2) return;

        var logicalPageToOwner = new Dictionary<int, (int objNum, int pageIdx)>();
        for (var oi = 0; oi < DumpObjects.Length; oi++)
        {
            var obj = DumpObjects[oi];
            if (GetObjectPageMapEntries(obj) == 0) continue;

            var startLogical = (int)GetObjectPageMapIndex(obj);
            for (var pi = 0; pi < GetObjectPageMapEntries(obj); pi++)
                logicalPageToOwner[startLogical + pi] = (oi + 1, pi);
        }

        foreach (var fixup in DumpFixupRecords)
        {
            if (!logicalPageToOwner.TryGetValue(fixup.LogicalPage, out var owner))
                continue;

            var (objNum, pageIdx) = owner;
            var symbol = ResolveFixupSymbol(fixup);

            RememberCodeReference(objNum, fixup);

            if (symbol is null) continue;

            // Extract target information for far-operand assembly
            var (targetObj, targetOff) = fixup.TargetData switch
            {
                FixupTargetInternal i => ((int)i.ObjectNumber, i.TargetOffset),
                _ => (0, 0u)
            };
            var isSelectorOnly = fixup.SourceType.AddressType == FixupAddressType.Selector16;

            var offsets = fixup.SourceType.HasSourceList
                ? fixup.SourceOffsetList
                : [fixup.SourceOffset];
            var size = GetFixupByteSize(fixup);

            foreach (var srcOff in offsets)
            {
                var objOff = pageIdx * PageSize + srcOff;
                if (FixupAt.ContainsKey((objNum, objOff))) continue;

                FixupAt[(objNum, objOff)] = new FixupSite(
                    objNum, objOff, size, symbol,
                    targetObj, targetOff, fixup.AdditiveValue, isSelectorOnly);
            }
        }
    }

    private static int GetFixupByteSize(FixupRecord fixup) => fixup.SourceType.AddressType switch
    {
        FixupAddressType.Offset16   => 2,
        FixupAddressType.Selector16 => 2,
        FixupAddressType.Offset32   => 4,
        FixupAddressType.OffsetRelEip32 => 4,
        //   raw ATP 001 = ptr16:16 = 4 bytes -> Far32
        //   raw ATP 011 = ptr16:32 = 6 bytes -> Far48
        FixupAddressType.Far   => 4,
        FixupAddressType.Far32 => 6,
        FixupAddressType.LowByte => 1,
        _ => 4
    };

    private void RememberCodeReference(int sourceObject, FixupRecord fixup)
    {
        if (fixup.TargetData is not FixupTargetInternal target) return;
        if (sourceObject < 1 || sourceObject > DumpObjects.Length) return;

        var targetObject = target.ObjectNumber;
        var targetOffset = (long)target.TargetOffset + (fixup.AdditiveValue ?? 0);

        if (targetObject < 1 || targetObject > DumpObjects.Length) return;
        if (targetOffset is <= 0 or > int.MaxValue) return;

        // Target must be .CODE
        if (!GetObjectExecute(DumpObjects[targetObject - 1])) return;

        var sourceIsCode = GetObjectExecute(DumpObjects[sourceObject - 1]);
        var addrType = fixup.SourceType.AddressType;

        // Far targets from any space is .CODE.
        // 2) Offset16/Offset32 from .DATA into .CODE -> callback / vtable
        var isCodeRef = IsCodePointerAddressType(addrType)
                        || (!sourceIsCode && addrType is FixupAddressType.Offset16 or FixupAddressType.Offset32);

        if (!isCodeRef) return;

        if (!FixupTargetsByObject.TryGetValue(targetObject, out var offsets))
        {
            offsets = [];
            FixupTargetsByObject[targetObject] = offsets;
        }

        offsets.Add((int)targetOffset);
    }

    /// <summary>
    /// Recursive-descent reachability within a single object.
    /// Operates in the object's local offset space (same as Targets / fixups / bundles).
    /// </summary>
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
        
        foreach (var seed in seeds)
            worklist.Push(seed);
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
            {
                if (target < 0) continue;
                if (instructionAt.ContainsKey(target))
                    worklist.Push(target);
            }
            if (IsUnconditionalBranch(instr))
                continue;
            var next = offset + instr.Length;
            if (instructionAt.ContainsKey(next))
                worklist.Push(next);
        }
        return reachable;
    }

    private static bool IsUnconditionalBranch(DecodedInstruction instr)
    {
        var m = instr.Flow.IsIndirectJump || instr.Flow.IsJump || instr.Flow.IsIndirectJumpFar;

        return m;
    }

    protected static bool IsCodePointerAddressType(FixupAddressType type) =>
        type is FixupAddressType.Far or FixupAddressType.Far32 or FixupAddressType.OffsetRelEip32;

    private string? ResolveFixupSymbol(FixupRecord fixup)
    {
        var additive = fixup.AdditiveValue ?? 0;

        string ResolveInternal(int objNum, int baseOffset)
        {
            var effective = Convert.ToInt32(baseOffset + additive);

            var export = ExportAt.GetValueOrDefault<(int objNum, int effective), string>((objNum, effective))
                   ?? $"FAR0x{objNum:X4}{effective:X4}";
            
            return $"{DumpResidentNames[0].String}::{export}";
        }

        return fixup.TargetData switch
        {
            FixupTargetInternal target =>
                ResolveInternal(target.ObjectNumber, (int)target.TargetOffset),
            FixupTargetImportOrdinal impOrd =>
                $"{GetModuleName(impOrd.ModuleIndex)}::@{impOrd.ImportOrdinal}",
            FixupTargetEntryTable ent => $"::EntryTable::@{ent.Ordinal}",
            _ => ResolveImportNameFixup(fixup)
        };
    }

    private string? ResolveImportNameFixup(FixupRecord fixup)
    {
        if (fixup.TargetData is not FixupTargetImportName impName) return null;
        var mod = GetModuleName(impName.ModuleIndex);
        var proc = GetProcedureName(impName.NameOffset);
        return string.IsNullOrEmpty(proc)
            ? $"{mod}::IMPROCTAB+0x{impName.NameOffset:X4}"
            : $"{mod}::{proc}";
    }

    private string GetModuleName(ushort moduleIndex)
    {
        var idx = moduleIndex - 1;
        if (idx >= 0 && idx < DumpImportRecords.Length)
            return DumpImportRecords[idx].DllName;
        return $"MOD#{moduleIndex}";
    }

    private string GetProcedureName(uint nameOffset)
    {
        var rec = DumpImportRecords.FirstOrDefault(x => x.Offset == nameOffset);
        return rec?.Name ?? string.Empty;
    }

    private static string FailSafe(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private void TranslateObject(int objectNumber, FileObject rawObj)
    {
        var is32Bit = (GetObjectObjectFlagsMask(rawObj) & 0x2000) != 0;
        var suggestedName = GetObjectSuggestedName(rawObj);
        var isExecutable = GetObjectExecute(rawObj);

        var bytes = BuildObjectBytes(rawObj);
        if (bytes is null || bytes.Length == 0)
        {
            EmitObjectHeader(objectNumber, suggestedName, rawObj, is32Bit, 0, 0, 0);
            Results.Add("; Uninitialized / BSS");
            EmitObjectFooter(objectNumber);
            return;
        }

        if (!isExecutable && !DiscoverDataProcedures)
        {
            EmitObjectHeader(objectNumber, suggestedName, rawObj, is32Bit, 0, 0, 0);
            Results.Add("; Non-executable object, kept as data");
            RenderDataObjectWithExports(objectNumber, bytes);
            DumpObjectStrings(objectNumber, bytes);
            EmitObjectFooter(objectNumber);
            return;
        }

        var seeds = CollectEntryPoints(objectNumber, bytes);

        if (seeds.Count == 0)
        {
            if (isExecutable)
                seeds.Add(0);
            else
            {
                EmitObjectHeader(objectNumber, suggestedName, rawObj, is32Bit, 0, 0, 0);
                Results.Add("; No code references into this object, kept as data");
                RenderDataObjectWithExports(objectNumber, bytes);
                DumpObjectStrings(objectNumber, bytes);
                EmitObjectFooter(objectNumber);
                return;
            }
        }

        // Pass #1: decode from seeds + jump table discovery
        var (allInstructions, bridgeEntries, jumpTableBytes) = 
            RunAnalysisLoop(bytes, seeds, is32Bit);

        // Pass #2: recursive descent
        var reachable = RunRecursiveDescent(allInstructions, seeds, bytes, jumpTableBytes);

        // Pass #3: filter unreachable
        var kept = allInstructions
            .Where(i => reachable.Contains(i.Offset))
            .OrderBy(i => i.Offset)
            .ToArray();

        // Pass #4: local labels
        BuildObjectLocalLabels(objectNumber, kept);

        var prologues = CountAddedPrologues(bytes, seeds, isExecutable);
        EmitObjectHeader(objectNumber, suggestedName, rawObj, is32Bit,
            bridgeEntries.Count, prologues, 0);

        if (kept.Length < allInstructions.Count)
            Results.Add($"; Filtered:    {allInstructions.Count - kept.Length} unreachable instructions removed");

        // Pass #5: render
        var analysed = AnalyseInstructions(objectNumber, kept, bridgeEntries, ModeOf(is32Bit));
        RenderObject(objectNumber, bytes, is32Bit, analysed);
        DumpObjectStrings(objectNumber, bytes);
        EmitObjectFooter(objectNumber);

        return;

        ProcessorMode ModeOf(bool o) => o 
            ? ProcessorMode.I386 
            : ProcessorMode.I286;
    }

    private void EmitObjectHeader(int objectNumber, string name, FileObject obj, bool is32Bit, int exportCount,
        int prologCount, int followed)
    {
        var modeLabel = is32Bit ? "32-bit" : "16-bit";
        var useDirective = is32Bit ? "USE32" : "USE16";

        Results.Add(";");
        Results.Add($";         Object#{objectNumber} ({name})");
        Results.Add($"; Memory Page: {GetObjectPageMapIndex(obj)} (uses {GetObjectPageMapEntries(obj)} pages)");
        Results.Add($"; Followed:    {followed}");
        Results.Add($"; Mode:        {modeLabel}");
        Results.Add($"; Export:      {exportCount} module exports");
        Results.Add($"; Prologues:   {prologCount}");
        Results.Add(";");
        Results.Add($"SEGMENT Object_{objectNumber} {useDirective} ; {name}");
        Results.Add("");
    }

    private void EmitObjectFooter(int objectNumber)
    {
        Results.Add("");
        Results.Add($"ENDS Object_{objectNumber}");
        Results.Add("");
    }
    
    private void RenderDataObjectWithExports(int objNumber, byte[] bytes)
    {
        var sb = new StringBuilder();
        var exports = ExportAt
            .Where(kv => kv.Key.obj == objNumber && kv.Key.off >= 0 && kv.Key.off < bytes.Length)
            .OrderBy(kv => kv.Key.off)
            .ToList();

        var cover = 0;
        foreach (var (key, name) in exports)
        {
            if (key.off > cover)
                RenderDataRange(sb, objNumber, bytes, cover, key.off - cover);

            sb.AppendLine(";");
            sb.AppendLine($"; Exported Data @ 0x{key.off:X4}");
            sb.AppendLine(";");
            sb.AppendLine($"{name}:");
            cover = key.off;
        }

        if (cover < bytes.Length)
            RenderDataRange(sb, objNumber, bytes, cover, bytes.Length - cover);

        Results.Add(sb.ToString());
    }
    /// <summary>
    /// Registers local labels for every branch/call target inside this object.
    /// Export names take priority and are not overwritten.
    /// Only labels for kept (reachable) instructions are registered.
    /// </summary>
    private void BuildObjectLocalLabels(int objectNumber, IReadOnlyList<DecodedInstruction> instructions)
    {
        var keptOffsets = new HashSet<int>(instructions.Select(i => i.Offset));

        foreach (var instr in instructions)
        {
            var isFar = instr.Flow.IsIndirectCallFar || instr.Flow.IsIndirectJumpFar;
            foreach (var target in instr.Targets)
            {
                if (target < 0) continue;
                // Skip phantom "segment-placeholder" targets produced by 9A xx xx 00 00
                if (isFar && target == 0) continue;
                if (!keptOffsets.Contains(target)) continue;

                var key = (objectNumber, target);
                if (ExportAt.ContainsKey(key)) continue;
                if (_localLabelAt.ContainsKey(key)) continue;

                _localLabelAt[key] = GetLabel(instr, target);
            }
        }

        string GetLabel(DecodedInstruction instruction, int target)
        {
            if (instruction.Flow.IsIndirectCallFar)
                return $"{ProcedurePrefix}_far_0x{objectNumber:X4}{target:X4}";
            if (instruction.Flow.IsIndirectJumpFar)
                return $"{LabelPrefix}_far_0x{objectNumber:X4}:{target:X4}";
            switch (instruction.Flow.IsJump)
            {
                case true when instruction.Bytes.Length < 4:
                    return $"{LabelPrefix}_0x{target:X4}";
                case true when instruction.Bytes.Length >= 4:
                    return $"{LabelPrefix}_0x{target:X8}";
            }

            var operandSize = instruction.Bytes.Length >= 4 ? 8 : 4;

            return $"{LabelPrefix}_0x" + target.ToString($"X{operandSize}");
        }
    }

    private void DumpObjectStrings(int objNumber, byte[] bytes)
    {
        if (!DumpStrings) return;
        var strings = DataObjectExtension.CollectAsciiStrings(bytes, MinStringLength);

        if (strings.Count == 0) return;

        Results.Add("");
        Results.Add($"; --- ASCII strings of Object#{objNumber} ({strings.Count}) ---");
        foreach (var (offset, text) in strings)
            Results.Add($"\tDB {"\"" + text + "\"",-40} ; {objNumber}:0x{offset:X4} len={text.Length} bytes");
    }

    private void EmitObjectHeader(int objectNumber, string name, FileObject obj, string modeLabel, int exportCount,
        int prologCount, int followed)
    {
        Results.Add(";");
        Results.Add($";         Object#{objectNumber} ({name}");
        Results.Add($"; Starts from  {obj.PageMapIndex} page (uses {obj.PageMapEntries} pages)");
        Results.Add($"; Followed:    {followed}");
        Results.Add($"; Mode:        {modeLabel}");
        Results.Add($"; Export:      {exportCount} module exports");
        Results.Add($"; Prologues:   {prologCount}");
        Results.Add(";");
    }

    private SortedSet<int> CollectEntryPoints(int objectNumber, byte[] bytes)
    {
        var entryPoints = new SortedSet<int>();

        if (objectNumber == MainObj)
            entryPoints.Add((int)MainEip);

        foreach (var bundle in DumpEntryBundles)
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
                if (off >= 0 && off < bytes.Length) entryPoints.Add(off);
            }
        }

        SeedFixupTargetsForObject(objectNumber, bytes, entryPoints);

        return entryPoints;
    }

    private int CountAddedPrologues(byte[] bytes, SortedSet<int> entryPoints, bool isExecutable)
    {
        if (isExecutable || !DiscoverDataProcedures) return 0;

        return DataObjectExtension
            .FindProcedurePrologues(bytes)
            .Count(prologue => entryPoints.Add(prologue));
    }

    private (IReadOnlyList<DecodedInstruction> instructions, SortedSet<int> entrySet, HashSet<int> jumpTableBytes)
        RunAnalysisLoop(byte[] bytes, SortedSet<int> entryPoints, bool is32Bit)
    {
        IReadOnlyList<DecodedInstruction> instrs = [];
        SortedSet<int> entries = [];
        var jumpTableBytes = new HashSet<int>();

        for (var pass = 0; pass < LeMaxAnalysisPasses; pass++)
        {
            (instrs, _, entries) = is32Bit
                ? DecoderBridge.Decode32(bytes, [.. entryPoints])
                : DecoderBridge.Decode16(bytes, [.. entryPoints]);

            var reachable = RunRecursiveDescent(instrs, entryPoints, bytes, jumpTableBytes);
            var addedNewTargets = DiscoverJumpTableTargets(
                instrs, 
                reachable, 
                bytes, 
                is32Bit, 
                entryPoints, 
                jumpTableBytes
            );

            if (!addedNewTargets)
                break;
        }

        return (instrs, entries, jumpTableBytes);
    }
    private static bool IsZeroPadding(byte[] bytes, int offset, int count = 4)
    {
        if (offset < 0 || offset + count > bytes.Length) return false;
        for (var i = 0; i < count; i++)
            if (bytes[offset + i] != 0) return false;
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
        
        foreach (var off in reachable)
            knownCodeOffsets.Add(off);

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
            if (nextCode > tableAddr)
                upperBound = nextCode;

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

            for (var b = tableAddr; b < pos; b++)
                jumpTableBytes.Add(b);

            foreach (var t in localTargets)
            {
                if (entryPoints.Add(t))
                {
                    knownCodeOffsets.Add(t);
                    addedAny = true;
                }
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
        while (end < memText.Length && Uri.IsHexDigit(memText[end]))
            end++;

        if (end == start) return false;
        return int.TryParse(memText[start..end], System.Globalization.NumberStyles.HexNumber, null, out displacement);
    }
    private void SeedFixupTargetsForObject(int objNumber, byte[] bytes, SortedSet<int> entryPoints)
    {
        if (!FixupTargetsByObject.TryGetValue(objNumber, out var refs))
            return;

        foreach (var r in refs.Where(r => r >= 0 && r < bytes.Length))
            entryPoints.Add(r);
    }

    private IReadOnlyList<AnalysedInstruction> AnalyseInstructions(
        int objNumber,
        IReadOnlyList<DecodedInstruction> instructions,
        SortedSet<int> entrySet,
        ProcessorMode mode)
    {
        var callTargets = new HashSet<int>(
            instructions
                .Where(i => i.Flow.Equals(FlowControl.Call))
                .SelectMany(i => i.Targets));

        var result = new List<AnalysedInstruction>(instructions.Count);

        foreach (var instr in instructions)
        {
            var label = ResolveLabel(objNumber, instr.Offset);
            var instrFixups = CollectFixupsForRange(objNumber, instr.Offset, instr.Length);
            var analysedOps = new List<AnalysedOperand>(instr.Operands.Length);

            foreach (var op in instr.Operands)
            {
                var opFixup = FindFixupForOperand(objNumber, instr.Offset, op, instrFixups);
                var text = FormatOperandWithSubstitution(objNumber, instr, op, opFixup, mode);
                analysedOps.Add(new AnalysedOperand(text, opFixup));
            }

            result.Add(new AnalysedInstruction(
                Instr: instr,
                Label: label,
                IsEntryPoint: entrySet.Contains(instr.Offset),
                IsCallTarget: callTargets.Contains(instr.Offset),
                Operands: analysedOps,
                AllFixups: instrFixups)
            );
        }

        return result;
    }

    private string? ResolveLabel(int objNumber, int offset)
    {
        if (objNumber == MainObj && offset == MainEip)
            return "main";

        if (ExportAt.TryGetValue((objNumber, offset), out var exportName))
            return exportName;

        if (_localLabelAt.TryGetValue((objNumber, offset), out var localName))
            return localName;

        return null;
    }

    private List<FixupSite> CollectFixupsForRange(int obj, int instrOffset, int instrLength)
    {
        var result = new List<FixupSite>();
        // Scan a small window before the instruction to catch multi-byte
        // fixups that start in the previous instruction and straddle.
        const int lookbackMax = 16;
        var scanStart = Math.Max(0, instrOffset - lookbackMax);
        var scanEnd = instrOffset + instrLength;

        for (var i = scanStart; i < scanEnd; i++)
        {
            if (!FixupAt.TryGetValue((obj, i), out var fixup)) continue;
            var fEnd = fixup.ObjOff + fixup.Size;
            if (fEnd > instrOffset && fixup.ObjOff < scanEnd)
                result.Add(fixup);
        }

        return result;
    }

    private static FixupSite? FindFixupForOperand(
        int obj, int instrOffset, DecodedOperand op, IReadOnlyList<FixupSite> instrFixups)
    {
        if (op.BytesLength <= 0 || instrFixups.Count == 0) return null;

        var opStart = instrOffset + op.BytesOffset;
        var opEnd = opStart + op.BytesLength;

        // Prefer a fixup that begins exactly at the operand start
        foreach (var f in instrFixups)
            if (f.ObjOff == opStart) return f;

        // Otherwise first fixup whose range overlaps the operand
        foreach (var f in instrFixups)
        {
            var fStart = f.ObjOff;
            var fEnd = fStart + f.Size;
            if (fStart < opEnd && fEnd > opStart) return f;
        }

        return null;
    }

    private string FormatOperandWithSubstitution(
        int objNumber,
        DecodedInstruction instr,
        DecodedOperand op,
        FixupSite? fixup,
        ProcessorMode mode)
    {
        // Far pointer needs special handling: combine object from fixup
        // with offset from code bytes when the fixup only patches the selector.
        if (op.Part is OperandPart.Far far)
            return FormatFarOperand(objNumber, far, fixup);

        // Non-far fixup substitution
        if (fixup is not null)
        {
            return op.Part switch
            {
                OperandPart.Imm => fixup.Symbol,
                OperandPart.SignedImm => fixup.Symbol,
                OperandPart.Abs => $"[{fixup.Symbol}]",
                OperandPart.Mem mem => ReplaceMemDisplacement(mem.Item, fixup.Symbol),
                _ => FormatOperandPlain(instr, op, mode)
            };
        }

        // Intra-object branch/call target
        if (instr.Targets.Count() == 1 && op.Part is OperandPart.Imm or OperandPart.SignedImm)
        {
            var target = instr.Targets[0];
            if (target >= 0
                && _localLabelAt.TryGetValue((objNumber, target), out var localLabel))
                return localLabel;
        }

        return FormatOperandPlain(instr, op, mode);
    }

    /// <summary>
    /// Resolves a far operand to a readable target.
    /// Handles two linker patterns:
    ///   A) code = "9A xx xx 00 00" (selector placeholder) + Selector16 fixup
    ///      -> real offset from code, real object from fixup
    ///   B) code = "9A 00 00 ss ss" (offset placeholder) + Offset/Far fixup
    ///      -> real offset from fixup (+ additive), real object from fixup
    /// </summary>
    private string FormatFarOperand(
        int objNumber, OperandPart.Far far, FixupSite? fixup)
    {
        var codeOffset = unchecked((int)far.Item2);

        if (fixup is null)
        {
            if (ExportAt.TryGetValue((objNumber, codeOffset), out var bare))
                return bare;
            if (FixupAt.TryGetValue((objNumber, codeOffset), out var nested))
                return nested.Symbol;
            return $"0x{codeOffset:X4}";
        }

        if (fixup.TargetObjNum == 0)
            return fixup.Symbol; // !!!
        
        var additive = (int)(fixup.Additive ?? 0);
        int targetObj, targetOff;

        if (fixup.IsSelectorOnly)
        {
            // Pattern A: selector is a placeholder, offset is real
            targetObj = fixup.TargetObjNum;
            targetOff = codeOffset + additive;
        }
        else
        {
            // Pattern B: offset comes from the fixup (plus additive)
            targetObj = fixup.TargetObjNum;
            targetOff = (int)fixup.TargetOffset + additive;
        }

        if (ExportAt.TryGetValue((targetObj, targetOff), out var name))
            return name;
        
        
        return $"0x{targetObj:X4}{targetOff:X4}";
    }

    private static string FormatOperandPlain(DecodedInstruction instr, DecodedOperand op, ProcessorMode mode) =>
        DecoderBridge.FormatOperandPart(mode, instr.Offset, op);

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

    private void RenderObject(
        int objNumber, byte[] bytes, bool is32Bit,
        IReadOnlyList<AnalysedInstruction> analysed)
    {
        var sb = new StringBuilder();
        var cover = 0;
        string? currentProcName = null;

        var ordered = analysed.OrderBy(a => a.Instr.Offset).ToArray();

        for (var idx = 0; idx < ordered.Length; idx++)
        {
            var analysedInstruction = ordered[idx];
            var instr = analysedInstruction.Instr;
            var startsNewProc = analysedInstruction.IsEntryPoint || analysedInstruction.IsCallTarget;

            if ((startsNewProc || instr.Offset > cover) && currentProcName is not null)
            {
                sb.AppendLine($"{currentProcName} ENDP");
                currentProcName = null;
            }

            if (instr.Offset > cover)
                RenderDataRange(sb, objNumber, bytes, cover, instr.Offset - cover);

            cover = Math.Max(cover, instr.Offset + instr.Length);

            if (startsNewProc)
            {
                var procName = analysedInstruction.Label
                               ?? $"{ProcedurePrefix}_0x{objNumber:X4}{instr.Offset:X4}";

                sb.AppendLine(";");
                var header = (analysedInstruction.IsEntryPoint, analysedInstruction.IsCallTarget) switch
                {
                    (true, true) => $"; Export CALL target @ 0x{instr.Offset:X4}",
                    (true, false) => $"; Export @ 0x{instr.Offset:X4}",
                    _ => $"; Procedure at 0x{instr.Offset:X4} instruction offset"
                };
                sb.AppendLine(header);
                sb.AppendLine(";");

                // NEAR / FAR concrete flags 16-bit code (32-bit FLAT is required just PROC)
                var distance = is32Bit
                    ? string.Empty
                    : (ExportAt.ContainsKey((objNumber, instr.Offset)) || (objNumber == MainObj && instr.Offset == MainEip)
                        ? " FAR"
                        : " NEAR");

                sb.AppendLine($"{procName} PROC{distance}");
                currentProcName = procName;
            }
            else if (analysedInstruction.Label is not null)
            {
                sb.AppendLine($"{analysedInstruction.Label}:");
            }

            var operandText = string.Join(", ", analysedInstruction.Operands
                .Select(o => o.Text)
                .Where(t => t.Length > 0));

            var fullText = operandText.Length > 0 ? $"{instr.Mnemonic} {operandText}" : instr.Mnemonic;
            var bytesStr = BitConverter
                .ToString(instr.Bytes)
                .Replace("-", " ");

            var xrefComment = analysedInstruction.AllFixups.Count > 0
                ? " xref: " + string.Join(", ", analysedInstruction.AllFixups
                    .Select(f => f.ObjOff == instr.Offset
                        ? f.Symbol
                        : $"{f.Symbol}+0x{f.ObjOff - instr.Offset:X}"))
                : string.Empty;

            var suffixComment = instr.Comment is not null && instr.Comment.Value.Length > 0
                ? instr.Comment.Value
                : string.Empty;

            sb.AppendLine($"\t{fullText,-30} ; {objNumber}:0x{instr.Offset:X4} {bytesStr}{suffixComment}{xrefComment}");

            if (instr.Flow.IsReturn)
            {
                var callerContext = currentProcName is not null
                    ? $"{currentProcName} "
                    : "previous ";
                sb.AppendLine($"; returned back to {callerContext}control flow");
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

        // Закрываем последнюю процедуру в объекте перед хвостом данных или ENDS
        if (currentProcName is not null)
        {
            sb.AppendLine($"{currentProcName} ENDP");
        }

        if (cover < bytes.Length)
            RenderDataRange(sb, objNumber, bytes, cover, bytes.Length - cover);

        Results.Add(sb.ToString());
    }

    private static void RenderDataRange(StringBuilder sb, int objNumber, byte[] bytes, int offset, int length)
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
                sb.AppendLine($"\tDB {runLength} DUP (0x00),".PadRight(30) + $" ; {objNumber}:0x{runStart:X4} padding");
                continue;
            }

            var pos = runStart;
            while (pos < mutableI && linesLeft > 0)
            {
                var take = Math.Min(bytesPerLine, mutableI - pos);
                var payload = string.Join(", ", bytes[pos..(pos + take)].Select(b => $"0x{b:X2}"));
                sb.AppendLine($"\tDB {payload},".PadRight(30) + $" ; {objNumber}:0x{pos:X4}");
                pos += take;
                linesLeft--;
            }
        }

        if (mutableI < limit)
            sb.AppendLine($"; {objNumber}:0x{mutableI:X4} {limit - mutableI} more byte(s) of data (omitted)");
    }
}