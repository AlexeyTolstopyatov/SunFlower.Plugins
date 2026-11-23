using SunFlower.Elf.Headers;

namespace SunFlower.Elf.Models;

public class ElfImageDetails
{
    public bool Is64Bit { get; set; }
    public bool IsLittleEndian { get; set; }

    public ElfType Type { get; set; }
    public ElfMachine Machine { get; set; }

    public ulong EntryPoint { get; set; }
    public ulong SectionHeaderOffset { get; set; }
    public ulong ProgramHeaderOffset { get; set; }

    public ushort SectionHeaderEntrySize { get; set; }
    public ushort SectionHeaderCount { get; set; }
    public ushort SectionNameStringTableIndex { get; set; }

    public ushort ProgramHeaderEntrySize { get; set; }
    public ushort ProgramHeaderCount { get; set; }

    public ElfSection[] Sections { get; set; } = [];
    public ElfSegment[] Segments { get; set; } = [];
    public ElfSymbolModel[] Symbols { get; set; } = [];
    public ElfDynamicEntry[] DynamicEntries { get; set; } = [];

    public string FilePath { get; set; } = string.Empty;

    public long FileLength { get; set; }
}