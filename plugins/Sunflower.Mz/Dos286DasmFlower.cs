// CoffeeLake (C) 2026-*
// 
// The Dos286DasmFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Abstractions;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

[Flower(FlowerTarget.Code)]
[FlowerVersionContract(5,0,0)]
public class Dos286DasmFlower : IFlower
{
    [Seed("", "")] 
    public string[] Result = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var decoderFactory = new DecoderFactory(InstructionSet.I80286);
        Result = decoderFactory.Decode(filePath);
    });

    public string Name => "Decode MZ Executable (i286)";
}