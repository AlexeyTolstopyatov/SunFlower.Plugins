using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

[FlowerSeedContract(5, 0, 0)]
[Flower(SeedTarget.Data)]
public class MarkZbikowskiFlowerSeed : IFlowerSeed
{
    public string Seed => "MZ Dump";
    public FlowerSeedStatus Status { get; } = new();
    public int Main(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".COM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(path), ".CMD", StringComparison.OrdinalIgnoreCase))
        {
            Status.IsEnabled = false;
            return 0;
        }
        try
        {
            MzDumpManager manager = new(path);
            MzTableManager tableManager = new(manager);
            
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.Regions));
            Status.IsEnabled = true;
        }
        catch (Exception e)
        {
            Status.LastError = e;
            Status.IsEnabled = false;
        }
        return 0;
    }
}