using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Models;

public class PeExportTableModel
{
    public PeImageExportDirectory ExportDirectory { get; set; }
    public List<ExportFunction> Functions { get; set; } = [];
}

public class ExportFunction
{
    public string Name { get; set; } = string.Empty;
    public uint Ordinal { get; set; }
    public ulong Address { get; set; }
}