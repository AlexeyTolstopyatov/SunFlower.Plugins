using System.Globalization;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using Sunflower.Dasm;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

[Flower(SeedTarget.Code)]
[FlowerSeedContract(5, 0,0)]
public class I86DisassemblerSeed : IFlowerSeed
{
    private readonly List<string> _results = [];
    /// <summary>
    /// Applies to the .COM/.CMD (CP/M 1x) programs. Usually used memory model was tiny
    /// That's why all image could be disassembled
    /// </summary>
    private void DecodeCom(ref string path)
    {
        var file = File.ReadAllBytes(path);
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        _results.AddRange([
            $"# SunFlower.MZ.dll for .COM files (at {DateTime.UtcNow})",
            $"# [{path}]",
            "# Was disassembled for I8086 including MS-DOS interrupt vectors",
            "# Connected external resources:",
            "#     - Architecture: opcodes8086.json",
            "#     - Interrupts: dos.json",
            "",
            "entry:"
        ]);
        // MS-DOS command always starts from zero.
        // If you expect to see something about PSP -> think different
        // File image contains raw bytes without any metadata like relocatable programs.
        // Just from the OS memory side -> When, MS-DOS tries to load DOS command -> organizes special padding (255 bytes)
        //
        // But from the side of file -> there's nothing the same.
        // Command file may contain x86 conditional jump to the entry point
        // And I'm expecting for this now. I specially tell you that zero file offset contains
        // near jump to the entry point. Or special commands block and further jump.
        _results.Add(I8086Decoder.decodeRecursive(interruptsPath, file, [0]));
    }
    private void DecodeSegment(ref string path, ref MzDumpManager manager, int ip, long sp)
    {
        // Open target file and try to disassemble selected code block
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        var file = File.ReadAllBytes(path);
        // Make beautiful header to the resulting file
        _results.AddRange([
            $"# SunFlower.MZ.dll for relocatable programs (at {DateTime.UtcNow})",
            $"# [{path}]", 
            "# Was disassembled for I8086 including MS-DOS interrupt vectors",
            "# Connected external resources:",
            "#     - Architecture: opcodes8086.json",
            "#     - Interrupts: dos.json",
            "# Defined MS-DOS program values from the program header are:",
            $"#    .CODE  offset: 0x{manager.CodeOffset:X}  (entry=0x{ip:X})",
            $"#    .STACK offset: 0x{manager.StackOffset:X} (stack=0x{sp:X})",
            "",
            "entry:"
        ]);
        // Process whole image bytes
        _results.Add(I8086Decoder.decodeRecursive(interruptsPath, file, [ip]));
    }

    private void DecodeDosStub(ref string path, int start, long end)
    {
        _results.AddRange([
            $"# SunFlower.MZ.dll for protected-mode executable (at {DateTime.UtcNow})",
            $"# [{path}]", 
            "# The DOS stub program ", 
            "# Disassembled for I8086 including MS-DOS interrupt vectors",
            "# Connected external resources:",
            "#     - Architecture: opcodes8086.json",
            "#     - Interrupts: dos.json",
            "",
            "entry:"
        ]);
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        using var fileStream = File.OpenRead(path);
        var dosStubBytes = new byte[Math.Abs(end - start)];
        fileStream.Position = start;
        fileStream.Read(dosStubBytes, 0, dosStubBytes.Length);
        
        _results.Add(I8086Decoder.decodeRecursive(interruptsPath, dosStubBytes, [0]));
    }
    public int Main(string path)
    {
        // If manger throws exception -> there's nothing to do. Data is missing
        if (string.Equals(Path.GetExtension(path), ".COM",  StringComparison.OrdinalIgnoreCase) 
            || string.Equals(Path.GetExtension(path), ".CMD", StringComparison.OrdinalIgnoreCase))
        {
            // Once problem: Windows "batch files" & OS/2 shell commands 
            // It might be .COM instead of large DOS executable
            // Then, decode all
            DecodeCom(ref path);
            // Save changes, exit
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, _results));
            Status.IsEnabled = true;
            return 0;
        }
        try
        {
            var manager = new MzDumpManager(path);
            // Given DOS 2.0 stub and protected-mode executable.
            // Trim the DOS stub (CS:IP doesn't need for this) and disassemble given bytes block
            if (manager.Header.e_lfanew != 0)
            {
                DecodeDosStub(ref path, manager.Header.e_lfarlc, manager.Header.e_lfanew);
                // Out of condition block instructions will be invalid for given DOS stub.
                // Usually it doesn't have any FAR pointers and executes as a compatible module
                // by special algorithm
                Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, _results));
                Status.IsEnabled = true;
                return 0;
            }
            // Code segment undefined. Can't disassemble given file
            // Usually, protected-mode executables don't have CS:IP pointer into IMAGE_DOS_HEADER
            // for else, .word [e_lfanew] ignores (= set to zero) and executable runs under DOS
            if (manager.Header.cs == 0)
                throw new Exception("Code segment is undeclared");
            // Insert special title at the top of file 
            var entryOffset = Convert.ToInt32(manager.CodeOffset + manager.Header.ip);
            var stackOffset = manager.StackOffset + manager.Header.sp;
            // Given DOS program. CS:IP defined. Decode code segment starting from entry point
            if (manager.Header.e_lfanew == 0)
                DecodeSegment(ref path, ref manager, entryOffset, stackOffset);
            Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, _results));
            Status.IsEnabled = true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Status.IsEnabled = false;
            Status.LastError = e;
        }
        return 0;
    }

    public string Seed => "DOS Executable (I8086 model)";
    public FlowerSeedStatus Status { get; } = new();
}