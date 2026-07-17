using System.Globalization;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using Sunflower.Dasm;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5, 0,0)]
public class DosIntel186DisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        var decoderFactory = new DecoderFactory(InstructionSet.I80186);
        
        Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, decoderFactory.Decode(path)));

        if (decoderFactory.DecoderException is not null)
        {
            Status.LastError = decoderFactory.DecoderException;
            Status.IsEnabled = false;
            return -1;
        }
        
        Status.IsEnabled = true;
        return 0;
    }

    public string Seed => "MZ Disassembler (only 186 instruction set)";
    public FlowerSeedStatus Status { get; } = new();
}