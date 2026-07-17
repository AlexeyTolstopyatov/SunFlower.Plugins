//
// CoffeeLake (C) 2026-*
// 
// The DosIntel286DisassemblerSeed.cs represents disassembler plugin
// which uses Intel 286 decoder API and given knowledge base from MarkZbikowskiFlowerSeed
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5, 0, 0)]
public class DosIntel286DisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        var decoderFactory = new DecoderFactory(InstructionSet.I80286);
        
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

    public string Seed => "MZ Disassembler (only 286 instruction set)";
    public FlowerSeedStatus Status { get; } = new();
}