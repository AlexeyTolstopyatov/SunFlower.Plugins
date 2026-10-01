// CoffeeLake (C) 2026-*
// 
// The Os2v2ExecutableDasmFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Abstractions;
using SunFlower.Le.Services;

namespace SunFlower.Le;

[Flower(FlowerTarget.Code)]
[FlowerVersionContract(5, 0, 0)]
public class Os2v2ExecutableDasmFlower : IFlower
{
    [Seed] // < emty constructor
    public string[] PseudoCode { get; private set; } = [];

    public Task CreateAsync(string filePath)
    {
        var dumpManager = new LxDumpManager(filePath);
        var disassembler = new LxDecoderService(filePath, dumpManager);
        
        PseudoCode = disassembler.Disassemble();
        
        return Task.CompletedTask;
    }

    public string Name => "Decode IBM OS/2 Linear eXecutable (i386)";
    public Exception Exception { get; }
}