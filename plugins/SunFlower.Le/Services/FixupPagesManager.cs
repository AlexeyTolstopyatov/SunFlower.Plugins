using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class FixupPagesManager(BinaryReader reader, uint pagesOffset, uint pagesCount, uint pagesShift)
{
    public List<FixupPageRecord> GetFixupPageOffsets()
    {
        reader.BaseStream.Position = pagesOffset;
        var items = new List<FixupPageRecord>();
        for (var i = 0; i < pagesCount + 1; ++i)
        {
            items.Add(new FixupPageRecord
            {
                Offset = reader.ReadUInt32()
            });
        }

        return items;
    }
}