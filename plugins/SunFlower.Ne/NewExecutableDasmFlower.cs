//
// CoffeeLake (C) 2026-*
// 
// The NewExecutableDasmFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using Sunflower.Dasm.Intel.Core;
using SunFlower.Ne.Services;

namespace SunFlower.Ne;

[Flower(FlowerTarget.Code, "asm")]
[FlowerVersionContract(5, 0, 0)]
public class NewExecutableDasmFlower : IFlower
{
    [Seed] 
    public string[] Result = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var dumpManager = new NeDumpManager(filePath);
        var set = ProcessorMode.I286;

        var decoder = new NeDecoderService(filePath, dumpManager, set);
        
        Result = decoder.Decode();
    });
    
    public string Name => "Decode New Executable (i286)";
}