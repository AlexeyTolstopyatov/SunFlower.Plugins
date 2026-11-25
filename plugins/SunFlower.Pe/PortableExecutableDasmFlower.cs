// CoffeeLake (C) 2026-*
//
// Decodes PE (IA-32) code sections into an assembly listing.
// Mirrors SunFlower.Le.LinearExecutableDasmFlower but for the PE format.

using SunFlower.Abstractions;
using SunFlower.Pe.Services;

namespace SunFlower.Pe;

[Flower(FlowerTarget.Code, "asm")]
[FlowerVersionContract(5, 0, 0)]
public class PortableExecutableDasmFlower : IFlower
{
    [Seed]
    public string[] Result { get; private set; } = [];

    public async Task CreateAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var dump = new PeDumpManager(filePath);
            var imports = new PeImportsManager(dump.ImageDetails, filePath);
            var exports = new PeExportsManager(dump.ImageDetails, filePath);
            var relocs = new PeBaseRelocationManager(dump.ImageDetails, filePath);
            
            imports.Dump();
            exports.Dump();

            var decoder = new PeDecoderService(filePath, dump, imports, exports, relocs);
            Result = decoder.Disassemble();
        });
    }

    public string Name => "Decode Portable Executable";
}
