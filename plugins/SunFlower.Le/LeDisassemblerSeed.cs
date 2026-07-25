//
// CoffeeLake (C) 2026-*
//
// LeDisassemblerSeed.cs — disassembler plugin entry point.
// Uses LeDecoderService (FAR addressing, no flat image).
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Services;

namespace SunFlower.Le;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5, 0, 0)]
public class LeDisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        try
        {
            var dumpManager = new LeDumpManager(path);
            var disassembler = new LeDecoderService(path, dumpManager);

            Status.Results.Add(new FlowerSeedResult(
                FlowerSeedEntryType.Strings, disassembler.Disassemble()));

            Status.IsEnabled = true;
            return 0;
        }
        catch (Exception e)
        {
            Status.LastError = e;
            return -1;
        }
    }

    public string Seed => "LE Disassembler (x86)";
    public FlowerSeedStatus Status { get; } = new();
}