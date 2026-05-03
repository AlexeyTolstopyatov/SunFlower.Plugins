using SunFlower.Mz.Services;

namespace Sunflower.Mz.Models;

public class MzHeaderModel : UnsafeManager
{
    public MzHeader Header { get; private set; }
    public List<MzRelocation> Relocations { get; private set; } = [];
    /// <summary>
    /// Returns just code segment file offset. Not entry point
    /// </summary>
    public long CodeOffset { get; init; }
    public long CodePointer { get; init; }
    /// <summary>
    /// Returns Stack segment file position. Not SS:SP
    /// </summary>
    public long StackOffset { get; init; }
    /// <summary>
    /// Computed file offset to the stack in the loading image 
    /// </summary>
    public long StackPointer { get; init; }
    public MzHeaderModel(BinaryReader reader)
    {
        var header = Fill<MzHeader>(reader);
        if (header.e_sign != 0x5a4d && header.e_sign != 0x4d5a)
        {
            throw new NotSupportedException($"File doesn't have DOS signature! (Got: 0x{header.e_sign:X})");
        }
        // As I need to know explicit file offsets in the "loading" image, lets compute it manually
        // MS-DOS uses PSP table to hold on process metadata, and it initializes
        // when program is loading into DOS "Program Memory"
        Header = header;
        CodeOffset = Header.cs != 0
            ? Header.e_cparhdr * 0x10 + Header.cs * 0x10 // e_cs * 16 + e_ip.
            : 0;
        StackOffset = Header.ss != 0
            ? Header.e_cparhdr * 0x10 + Header.ss * 0x10
            : Header.cs;
        // If stack is undeclared, DOS allocates .STACK by next idea:
        // SS := CS (DS set to PSP base, ES, GS set to PSP base)
        // SP := SP = (e_cparhdr - 1) * 16 - 2
        CodePointer = CodeOffset + Header.ip;
        if (Header is { ss: 0, sp: 0 })
            StackPointer = (Header.e_cparhdr - 1) * 0x10 - 2;
        
        if (Header.e_relc == 0)
            return;
        
        reader.BaseStream.Position = Header.e_lfarlc;
        
        for (ushort c = 0; c < Header.e_relc; ++c)
        {
            Relocations.Add(Fill<MzRelocation>(reader));
        }
        
        // That's all. 
    }
}