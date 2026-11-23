namespace SunFlower.Pe.Models;

public class ImportNameTable
{ 
    // other need fields here . (structure like ExportTable)
    public List<ImportModule> Modules { get; set; } = [];

}

public class ImportModule
{
    public List<ImportFunction> Functions { get; init; } = [];
}

public class ImportFunction
{
    public string Module { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint Ordinal { get; set; }
    public ushort Hint { get; set; }
    public ulong Address { get; set; }
}