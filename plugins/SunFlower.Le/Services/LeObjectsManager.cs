using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class LeObjectsManager
{
    public List<FileObject> Objects { get; set; } = [];

    public LeObjectsManager(BinaryReader reader, uint offset, uint count)
    {
        reader.BaseStream.Position = offset;
        
        for (var i = 0; i < count; i++)
        {
            var virtualSegmentSize = reader.ReadUInt32();
            var relocationBase = reader.ReadUInt32();
            var objectFlagsMask = reader.ReadUInt32();
            var pageMap = reader.ReadUInt32();
            var pageMapEntries = reader.ReadUInt32();
            var unknownField = reader.ReadUInt32();
            
            Objects.Add(new(
                virtualSegmentSize, 
                relocationBase,
                objectFlagsMask, 
                pageMap, 
                pageMapEntries, 
                unknownField)
            );
        }
    }
}