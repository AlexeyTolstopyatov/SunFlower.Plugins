using SunFlower.Le.Services;

namespace SunFlower.Debug;

class Program
{
    /// <summary>
    /// Debug harness for the Linear Executable decoders.
    /// Usage: SunFlower.Debug [path] [le|lx] [nodata] [nostrings] [noprologues]
    /// </summary>
    static void Main(string[] args)
    {
        var dest = args.Length > 0 ? args[0] : @"D:\TEST\MS_OS220\DOSCALL1.DLL";
        // Linear executable object format modification/revision 
        var modification = (args.Length > 1 ? args[1] : "le").ToLowerInvariant();

        var analyzeData = !args.Any(a => a.Equals("nodata", StringComparison.OrdinalIgnoreCase));
        var dumpStrings = !args.Any(a => a.Equals("nostrings", StringComparison.OrdinalIgnoreCase));
        var discoverProcedures = !args.Any(a => a.Equals("noprologues", StringComparison.OrdinalIgnoreCase));

        string[] res;

        if (modification == "lx")
        {
            var dump = new LxDumpManager(dest);
            var service = new LxDecoderService(dest, dump)
            {
                AnalyseDataObjects = analyzeData,
                DumpStrings = dumpStrings,
                DiscoverDataProcedures = discoverProcedures
            };

            res = service.Disassemble();
        }
        else
        {
            var dump = new LeDumpManager(dest);
            var service = new LeDecoderService(dest, dump)
            {
                AnalyseDataObjects = analyzeData,
                DumpStrings = dumpStrings,
                DiscoverDataProcedures = discoverProcedures
            };

            res = service.Disassemble();
        }

        var outName = Path.GetFileNameWithoutExtension(dest) + (modification == "lx" ? "_LX.asm" : "_LE.asm");
        var outPath = Path.Combine(AppContext.BaseDirectory, outName);

        File.WriteAllText(outPath, string.Join("\n", res));
        Console.WriteLine($"[OK] lines={res.Length} -> {outPath}");
    }
}