using SunFlower.Elf.Headers;

namespace SunFlower.Elf.Models;

/// <summary>
/// Model representation of an ELF program header (segment),
/// suitable for display in the SunFlower client.
/// </summary>
public class ElfSegment
{
    public ElfSegmentType Type { get; set; }
    public ElfSegmentFlags Flags { get; set; }
    public ulong Offset { get; set; }
    public ulong VirtualAddress { get; set; }
    public ulong PhysicalAddress { get; set; }
    public ulong FileSize { get; set; }
    public ulong MemorySize { get; set; }
    public ulong Align { get; set; }

    public bool IsExecutable => (Flags & ElfSegmentFlags.Execute) != 0;
    public bool IsWritable => (Flags & ElfSegmentFlags.Write) != 0;
    public bool IsReadable => (Flags & ElfSegmentFlags.Read) != 0;

    public override string ToString() =>
        $"{Type} (0x{VirtualAddress:X}+0x{MemorySize:X}) @ file 0x{Offset:X}";
}