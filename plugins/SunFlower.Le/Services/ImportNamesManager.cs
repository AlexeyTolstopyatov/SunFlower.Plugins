using System.Text;

namespace SunFlower.Le.Services;

public class ImportNamesManager
{
    public List<string> ImportingModules { get; } = [];
    public ImportNamesManager(BinaryReader reader, uint modulesOffset, uint modulesCount)
    {
        reader.BaseStream.Position = modulesOffset;
        for (uint i = 0; i < modulesCount; i++)
        {
            var size = reader.ReadByte();
            ImportingModules.Add(Encoding.ASCII.GetString(reader.ReadBytes(size)));
        }
    }
}