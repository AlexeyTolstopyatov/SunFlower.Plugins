using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class FixupPagesManager(BinaryReader reader, uint pagesOffset, uint pagesCount)
{
    public List<FixupPageRecord> GetFixupPageOffsets()
    {
        reader.BaseStream.Position = pagesOffset;
#if DEBUG
        FixupDebug.Log($"[FixupPages] table @ 0x{pagesOffset:X8}, entries = {pagesCount + 1}");
        FixupDebug.DumpHex(reader, pagesOffset, (int)(pagesCount + 1) * 4, "[FixupPages raw]");
#endif
        var items = new List<FixupPageRecord>();
        for (var i = 0; i < pagesCount + 1; ++i)
        {
            var off = reader.ReadUInt32();
#if DEBUG
            FixupDebug.Log($"[FixupPages]   [{i}] = 0x{off:X8}");
#endif
            items.Add(new FixupPageRecord { Offset = off });
        }

        return items;
    }
}