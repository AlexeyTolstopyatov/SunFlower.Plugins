// CoffeeLake (C) 2026-*
// 
// The LxDisassemblerSeed.cs represents disassembler extension
// for application of IBM OS/2 2.0+-4.52, eComStation, ArcaOS operating systems 
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Services;

namespace SunFlower.Le;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5,0,0)]
public class LxDisassemblerSeed : IFlowerSeed
{
    public int Main(string path)
    {
        try
        {
            var dumpManager = new LxDumpManager(path);
            var disassembler = new LxDecoderService(path, dumpManager);

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

    public string Seed => "LX Disassembler (x86)";
    public FlowerSeedStatus Status { get; } = new();
}