using SunFlower.Abstractions;

namespace SunFlower.Ne;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(4, 0, 0)]
public class NewDisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        
        return 0;
    }

    public string Seed => "NewExecutable Disassembler (x86)";
    public FlowerSeedStatus Status { get; } = new();
}