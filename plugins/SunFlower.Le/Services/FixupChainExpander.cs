// CoffeeLake (C) 2026-*
// 
// The FixupChainExpander.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public static class FixupChainExpander
{
    private const ushort ChainEndMarker   = 0xFFF;
    private const uint   SourceOffsetMask = 0x0FFF;

    /// <summary>
    /// Expands Internal Chaining Fixups in fixup records
    /// </summary>
    /// <param name="records">Fixup records (presented already LogicalPage).</param>
    /// <param name="reader">BinaryReader active instance in SET SPECIAL POSITION</param>
    /// <param name="pageFileOffsets">
    /// File offsets from the beginning of data page. 0-based.
    /// If page offset is unable to compute -> -1 skips all fixup chain.
    /// </param>
    public static List<FixupRecord> Expand(
        IEnumerable<FixupRecord> records,
        BinaryReader reader,
        IReadOnlyList<long> pageFileOffsets)
    {
        var result   = new List<FixupRecord>();
        var savedPos = reader.BaseStream.Position;

        try
        {
            foreach (var record in records)
            {
                result.Add(record);

                if (!IsChainingFixup(record))
                    continue;

                if (record.TargetData is not FixupTargetInternal target)
                    continue;

                if (record.LogicalPage < 0 ||
                    record.LogicalPage >= pageFileOffsets.Count)
                    continue;

                var pageOffset = pageFileOffsets[record.LogicalPage];
                if (pageOffset < 0)
                    continue;

                TraverseChain(record, target, reader, pageOffset, result);
            }
        }
        finally
        {
            reader.BaseStream.Position = savedPos;
        }

        return result;
    }

    private static void TraverseChain(
        FixupRecord initial,
        FixupTargetInternal target,
        BinaryReader reader,
        long pageFileOffset,
        List<FixupRecord> output)
    {
        var visited    = new HashSet<ushort> { initial.SourceOffset };
        var currentSrc = initial.SourceOffset;
        var streamLen  = reader.BaseStream.Length;

        while (true)
        {
            var pos = pageFileOffset + currentSrc;
            if (pos < 0 || pos + 4 > streamLen)
                return;

            reader.BaseStream.Position = pos;
            var chainData = reader.ReadUInt32();

            var nextSrc = (ushort)(chainData & SourceOffsetMask);
            var nextTrg = chainData >> 12;

            if (nextSrc == ChainEndMarker)
                return;

            if (!visited.Add(nextSrc))
                return;

            output.Add(new FixupRecord(
                initial.SourceType,
                initial.RelocationFlags,
                nextSrc,
                new FixupTargetInternal(target.ObjectNumber, nextTrg),
                initial.AdditiveValue,
                [],
                logicalPage: initial.LogicalPage));

            currentSrc = nextSrc;
        }
    }

    private static bool IsChainingFixup(FixupRecord record) =>
        record.RelocationFlags.RelocationType == FixupRelocationType.InternalChaining
        || record.RelocationFlags.IsInternalChaining;
}