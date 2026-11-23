using SunFlower.Le.Services;
using SunFlower.Pe.Services;

namespace SunFlower.Debug;

class Program
{
    static void Main(string[] args)
    {
        //DecodeWindows9xExecutable();
        //DecodeOs2Executable();
        DecodeElf32();
    }

    private static void DecodeOs2Executable()
    {
        var dest = @"D:\TEST\IBM_OS220\DOSCALL1.DLL";

        var decoder = DecoderServiceFactory.Create(dest, new LxDumpManager(dest));
        var result = decoder.Disassemble();
        
        File.WriteAllLines(Path.Combine("D:\\", Path.GetFileNameWithoutExtension(dest)), result);
    }

    private static void DecodeElf32()
    {
        var dest = @"D:\TEST\Cent6.5-386\cat";

        var decoder = new Elf.Services.ElfDecoderService(dest, new Elf.Services.ElfDumpManager(dest));
        var result = decoder.Disassemble();
        
        File.WriteAllLines(Path.Combine("D:\\", Path.GetFileNameWithoutExtension(dest)), result);
    }

    private static void DecodeWindows9xExecutable()
    {
        var dest = @"C:\Users\atvlg\OneDrive\Desktop\winhlp32.exe";
        
        var dump = new PeDumpManager(dest);
        var imports = new PeImportsManager(dump.ImageDetails, dest);
        var exports = new PeExportsManager(dump.ImageDetails, dest);
        var relocs = new PeBaseRelocationManager(dump.ImageDetails, dest);
        
        imports.Dump();
        exports.Dump();

        var decoder = new PeDecoderService(dest, dump, imports, exports, relocs);
        var strings = decoder.Disassemble();
        
        File.WriteAllLines(Path.Combine("D:\\", Path.GetFileNameWithoutExtension(dest)), strings);
    }
}