using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Services;

namespace SunFlower.Le;
[FlowerSeedContract(4,5,0)]
public class LinearExecutableFlowerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        try
        {
            LeDumpManager dumpManager = new(path);
            LeTableManager tableManager = new(dumpManager);

            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, tableManager.Characteristics));

            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.Headers));
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.ObjectRegions));
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.EntryTableRegions));
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Regions, tableManager.NamesRegions));
            
            Status.IsEnabled = true;

            return 0;
        }
        catch (Exception e)
        {
            Status.LastError = e;
            return -1;
        }
    }

    public string Seed => "Sunflower OS/2-Windows386 LE x86";
    public FlowerSeedStatus Status { get; } = new();
}