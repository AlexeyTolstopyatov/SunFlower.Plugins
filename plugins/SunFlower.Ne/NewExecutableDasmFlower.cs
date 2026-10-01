//
// CoffeeLake (C) 2026-*
// 
// The NewExecutableDasmFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Ne.Services;

namespace SunFlower.Ne;

[Flower(FlowerTarget.Code)]
[FlowerVersionContract(5, 0, 0)]
public class NewExecutableDasmFlower : IFlower
{
    [Seed] 
    public string[] Result = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var dumpManager = new NeDumpManager(filePath);
        const NeInstructionSet set = NeInstructionSet.Intel286; // Later define the CPU flags correct

        var decoder = new NeDecoderService(filePath, dumpManager, set);
        
        Result = decoder.Decode();
    });
    
    public string Name => "Decode New Executable (i286)";
}