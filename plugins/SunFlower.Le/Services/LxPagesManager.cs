using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Services;

public class LxPagesManager
{
    public List<ObjectPage> Pages { get; } = [];

    public LxPagesManager(BinaryReader reader, uint offset, uint count)
    {
        reader.BaseStream.Position = offset;
        
        for (var i = 0; i < count; i++)
        {
            var entry = new ObjectPage
            {
                PageOffset = reader.ReadUInt32(),
                DataSize = reader.ReadUInt16(),
                Flags = reader.ReadUInt16()
            };
            Pages.Add(entry);
        }
    }
}