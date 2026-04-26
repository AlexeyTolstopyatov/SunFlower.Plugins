using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Ne.Services;

namespace SunFlower.Ne;

[Flower(SeedTarget.Data)]
[FlowerSeedContract(5,0,0)]
public class NewExecutableSeed : IFlowerSeed
{
    public string Seed => "Sunflower Win16-OS/2 NE IA-32";
    public FlowerSeedStatus Status { get; } = new();
    public int Main(string path)
    {
        try
        {
            NeDumpManager dumpManager = new(path);
            NeTableManager tableManager = new(dumpManager);

            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.MainRegions));
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, tableManager.Characteristics));
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.NestedDataRegions));
            
            Status.IsEnabled = true;
            return 0;
        }
        catch (Exception e)
        {
            Status.LastError = e;
            return -1;
        }
    }
}