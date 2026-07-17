//
// CoffeeLake (C) 2026-*
// 
// The DecoderFactory.cs represents disassembler factory method 
// for various instructions sets for PC/MS-DOS relocatable executable programs 
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Diagnostics;
using Sunflower.Dasm;

namespace Sunflower.Mz.Services;

public enum InstructionSet
{
    I8086,
    I80186,
    I80286
}
public class DecoderFactory(InstructionSet set)
{
    private readonly List<string> _results = [];
    
    public Exception? DecoderException { get; private set; }
    /// <summary>
    /// Applies to the .COM programs. Usually used memory model was tiny
    /// That's why all image could be disassembled
    /// </summary>
    private void DecodeCom(ref string path)
    {
        var file = File.ReadAllBytes(path);
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        _results.AddRange([
            $"; SunFlower.MZ.dll for .COM files (at {DateTime.UtcNow})",
            $"; [{path}]",
            $"; Was disassembled for Intel {set} including MS-DOS interrupt vectors",
            "; Connected external resources:",
            ";     - Interrupts: dos.json",
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
        Decode(interruptsPath, file, [0]);
    }
    private void DecodePages(ref string path, ref MzDumpManager manager, int entryPoint, int stackPoint)
    {
        // Open target file and try to disassemble selected code block
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        var file = File.ReadAllBytes(path);
        
        // Make beautiful header to the resulting file
        _results.AddRange([
            $"; SunFlower.MZ.dll for relocatable programs (at {DateTime.UtcNow})",
            $"; [{path}]", 
            $"; Was disassembled for Intel {set} including MS-DOS interrupt vectors",
            "; Connected external resources:",
            ";     - Interrupts: dos.json",
            "; Defined MS-DOS program values from the program header are:",
            $";    .CODE  offset: 0x{manager.CodeOffset:X}  (entry=0x{entryPoint:X})",
             ";           Entry point automatically set to 0x1C if computed file pointer is zero.",
            $";    .STACK offset: 0x{manager.StackOffset:X} (stack=0x{stackPoint:X})",
            "",
            "entry:"
        ]);
        
        Decode(interruptsPath, file, [entryPoint]);
    }

    private void DecodeDosStub(ref string path, int start, long end)
    {
        _results.AddRange([
            $"; SunFlower.MZ.dll for protected-mode executable (at {DateTime.UtcNow})",
            $"; [{path}]", 
            "; The DOS stub program ", 
            $"; Disassembled for {set} including MS-DOS interrupt vectors",
            "; Connected external resources:",
            ";     - Interrupts: dos.json",
            "",
            "entry:"
        ]);
        var interruptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "dos.json");
        using var fileStream = File.OpenRead(path);
        var dosStubBytes = new byte[Math.Abs(end - start)];
        fileStream.Position = start;
        fileStream.ReadExactly(dosStubBytes, 0, dosStubBytes.Length);

        Decode(interruptsPath, dosStubBytes, [0]);
    }

    private void Decode(string path, byte[] imageBytes, int[] entryPoints)
    {
        var text = set switch
        {
            InstructionSet.I8086 => I8086Decoder.decodeRecursive(path, imageBytes, entryPoints),
            InstructionSet.I80186 => I80186Decoder.decodeRecursive(path, imageBytes, entryPoints),
            InstructionSet.I80286 => I80286Decoder.decodeRecursive(path, imageBytes, entryPoints),
            _ => throw new UnreachableException()
        };
        _results.Add(text);
    }
    public string[] Decode(string path)
    {
        // If manger throws exception -> there's nothing to do. Data is missing
        if (string.Equals(Path.GetExtension(path), ".COM",  StringComparison.OrdinalIgnoreCase))
        {
            DecodeCom(ref path);
            // Save changes, exit
            
            // Status.Results.Add(new FlowerSeedResult(FlowerSeedEntryType.Strings, _results));
            // Status.IsEnabled = true;
            return _results.ToArray();
        }
        try
        {
            var manager = new MzDumpManager(path);
            // Given DOS 2.0 stub and protected-mode executable.
            // Trim the DOS stub (CS:IP doesn't need for this) and disassemble given bytes block
            // There's many reasons to think "what is actually might be a DOS stub entry offset?"
            // 
            // But traditionally, exists two conditions:
            //      1) e_lfarlc sets to 0x40. (Actually since the 0x40 starts DOS stub instead of relocations table)
            //      2) e_lfanew not zero. (Sets to protected-mode executable header)
            // New executables made by Microsoft LINK.EXE are ignoring the half of conditions. (!!!)
            if (manager.Header.e_lfanew != 0 && manager.Header.e_lfarlc == 0x40)
            {
                DecodeDosStub(ref path, manager.Header.e_lfarlc, manager.Header.e_lfanew);
                // Out of condition block instructions will be invalid for given DOS stub.
                // Usually it doesn't have any FAR pointers and executes as a compatible module
                // by special algorithm
                return _results.ToArray();
            }
            // The earlier MZ-style EXE, like COM, also is loaded on top of a PSP.
            // The gap between COM and EXE can be bridged by making the entry point CS:IP = fff0:0100,
            // which 16-bit math treats as -10:0100. Then fff0:0000 is the location of the PSP,
            // while the logical address for the entry point (S0+fff0):0100, after relocation goes to the same physical
            // address as does the logical address (S0+0000):0000 of the top of the program image.
            var stackOffset = manager.StackOffset + manager.Header.sp;
            // Given DOS program. CS:IP defined. Decode code segment starting from entry point
            DecodePages(ref path, ref manager, (int)manager.CodePointer, (int)stackOffset);
        }
        catch (Exception e)
        {
            DecoderException = e;
        }
        
        return _results.ToArray();
    }
}