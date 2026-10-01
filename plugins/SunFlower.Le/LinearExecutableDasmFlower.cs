// CoffeeLake (C) 2026-*
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Abstractions;
using SunFlower.Le.Services;

namespace SunFlower.Le;

[Flower(FlowerTarget.Code)]
[FlowerVersionContract(5, 0, 0)]
public class LinearExecutableDasmFlower : IFlower
{
    [Seed] 
    public string[] Result { get; private set; } = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var dumpManager = new LeDumpManager(filePath);
        var disassembler = new LeDecoderService(filePath, dumpManager);

        Result = disassembler.Disassemble();
    });
    
    public string Name => "Decode MS OS/2 Linear Executable (i386)";
}