using SunFlower.Elf.Headers;

namespace SunFlower.Elf.Models;

/// <summary>
/// Model representation of an ELF section header,
/// suitable for display in the SunFlower client.
/// </summary>
public class ElfSection
{
    public string Name { get; set; } = string.Empty;
    public ElfSectionType Type { get; set; }
    public ElfSectionFlags Flags { get; set; }
    public ulong Address { get; set; }
    public ulong Offset { get; set; }
    public ulong Size { get; set; }
    public uint Link { get; set; }
    public uint Info { get; set; }
    public ulong AddrAlign { get; set; }
    public ulong EntSize { get; set; }

    public bool IsExecutable => (Flags & ElfSectionFlags.ExecInstr) != 0;

    public bool IsWritable => (Flags & ElfSectionFlags.Write) != 0;

    public bool IsAllocated => (Flags & ElfSectionFlags.Alloc) != 0;

    public override string ToString() =>
        $"{Name} (0x{Address:X}+0x{Size:X}) @ file 0x{Offset:X}";
}