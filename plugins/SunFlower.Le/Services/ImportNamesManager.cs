namespace SunFlower.Le.Services;

public class ImportNamesManager
{
    public List<string> ImportingModules { get; } = [];
    public ImportNamesManager(BinaryReader reader, uint modulesOffset)
    {
        reader.BaseStream.Position = modulesOffset;
        var size = reader.ReadByte();
        while (size != 0)
        {
            ImportingModules.Add(new string(reader.ReadChars(size)));
            size = reader.ReadByte();
        }
    }
}