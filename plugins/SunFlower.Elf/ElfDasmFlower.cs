// CoffeeLake (C) 2026-*
//
//

using SunFlower.Abstractions;
using SunFlower.Elf.Services;

namespace SunFlower.Elf;

[Flower(FlowerTarget.Code, "asm")]
[FlowerVersionContract(5, 0, 0)]
public class ElfDasmFlower : IFlower
{
    [Seed]
    public string[] Result { get; private set; } = [];

    public async Task CreateAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var dump = new ElfDumpManager(filePath);
            var decoder = new ElfDecoderService(filePath, dump);
            Result = decoder.Disassemble();
        });
    }

    public string Name => "Decode ELF32/ELF64 (x86)";
}