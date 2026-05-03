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
    /// Inserts <c>Assets/header.asm</c> and rewrites some fields into the strings array
    /// </summary>
    private void InsertHeader(ref string path)
    {
        var headerLines =
            File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Assets", "header.asm"));
        
        _results.AddRange(headerLines);
    }
    /// <summary>
    /// Applies to the .COM/.CMD (CP/M 1x) programs. Usually used memory model was tiny
    /// That's why all image could be disassembled
    /// </summary>
    private void DecodeCom(ref string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);

        var decoder = I8086Decoder.get();
        var rl0 = reader.ReadBytes(3);
        var jumper = I8086Decoder.touchOperation(decoder, rl0);
        if (jumper == null)
            throw new Exception("Jumper is null. Can't continue disassembling");
        // if defined JMPN -> define file offset -> move to the code segment
        var offset = Convert.ToUInt16(jumper.Value.Item1.Split(' ')[1], 16);
        file.Position = offset;
        _results.Add($"entry: # <-- Defined entry point by the 0x{offset:X4} file offset");
        var image = reader.ReadBytes((int)(file.Length - offset));
        _results.AddRange(I8086Decoder.decode(image));
    }
    private void DecodeSegment(ref string path, long start, long end)
    {
        // Open target file and try to disassemble selected code block
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        
        file.Position = start;
        var codeLength = (end - start);
        // What if length is invalid?
        if (codeLength > reader.BaseStream.Length ||  codeLength < 0)
            codeLength = file.Length - start;
        
        var bytes = reader.ReadBytes((int)codeLength);
        
        _results.Add("SEGMENT .CODE START");
        _results.AddRange(I8086Decoder.decode(bytes));
        _results.Add("ENDS");
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
            // Code segment undefined. Can't disassemble given file
            // Usually, protected-mode executables don't have CS:IP pointer into IMAGE_DOS_HEADER
            // for else, .word [e_lfanew] ignores (= set to zero) and executable runs under DOS
            if (manager.Header.cs == 0)
                throw new Exception("Code segment is undeclared");
            // Insert special title at the top of file 
            InsertHeader(ref path);
            var entryOffset = manager.CodeOffset + manager.Header.ip;
            var stackOffset = manager.StackOffset + manager.Header.sp;
            _results.Add("# MZ header summary: ");
            _results.Add($"#     .CODE  offset: 0x{manager.CodeOffset:X}");
            _results.Add($"#     .STACK offset: 0x{manager.StackOffset:X}");
            _results.Add($"entry: # Executable entry point defined at the {entryOffset:X}");
            DecodeSegment(ref path, entryOffset, stackOffset);
            
            if (manager.Header.ss != 0)
                _results.Add($"# stack: {stackOffset:X}");
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