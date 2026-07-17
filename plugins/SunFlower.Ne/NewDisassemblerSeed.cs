//
// CoffeeLake (C) 2026-*
//
// The NewDisassemblerSeed.cs represents disassembler plugin
// which uses Intel 286/386 decoder API and knowledge base from NewExecutableSeed.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Dynamic;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Ne.Services;

namespace SunFlower.Ne;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5, 0, 0)]
public class NewDisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        try
        {
            // First, parse the NE structure (reuse existing managers)
            var dumpManager = new NeDumpManager(path);

            // Detect CPU from NE flags
            var cpu = dumpManager.NeHeader.NE_Flags;
            
            var set = NeInstructionSet.Intel286; // Later define the CPU flags correct

            var decoder = new NeDecoderService(path, dumpManager, set);

            Status.Results.Add(new FlowerSeedResult(
                FlowerSeedEntryType.Strings, decoder.Decode()));

            Status.IsEnabled = true;
            return 0;
        }
        catch (Exception e)
        {
            Status.LastError = e;
            return -1;
        }
    }

    public string Seed => "NE Disassembler (x86)";
    public FlowerSeedStatus Status { get; } = new();
}