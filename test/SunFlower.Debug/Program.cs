using SunFlower.Le.Services;

namespace SunFlower.Debug;

using SunFlower.Le;

class Program
{
    static void Main(string[] args)
    {
        var dest = @"D:\TEST\MS_OS220\DOSCALL1.DLL";
        
        var dump = new LeDumpManager(dest);
        var diasm = new LeDecoderService(dest, dump);
        
        var res = diasm.Disassemble();
        
        Console.WriteLine();
    }
}