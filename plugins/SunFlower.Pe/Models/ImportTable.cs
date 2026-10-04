namespace SunFlower.Pe.Models;

public class ImportTable
{ 
    // other need fields here . (structure like ExportTable)
    public List<ImportModule> Modules { get; set; } = [];

}

public class ImportModule
{
    public List<ImportedFunction> Functions { get; init; } = [];
}

public class ImportedFunction
{
    public string Module { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint Ordinal { get; set; } // Changed u64 -> u32
    public ushort Hint { get; set; }
    public ulong Address { get; set; }
}