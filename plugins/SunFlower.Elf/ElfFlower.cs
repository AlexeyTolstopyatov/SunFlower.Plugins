// CoffeeLake (C) 2026-*
//
// The ElfFlower.cs represents pure ELF32/ELF64 dumping plugin.
// Mirrors SunFlower.Pe.PortableExecutableFlower but for the ELF format.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using SunFlower.Abstractions;
using SunFlower.Elf.Headers;
using SunFlower.Elf.Models;
using SunFlower.Elf.Services;

namespace SunFlower.Elf;

[Flower(FlowerTarget.Data)]
[FlowerVersionContract(5, 0, 0)]
public class ElfFlower : IFlower
{
    [Seed(
        name: "ELF Header",
        description: "The ELF file header identifies the object as an executable, shared object, " +
                     "relocatable file or core dump. It describes the machine class (32/64-bit), " +
                     "byte order, entry point, section/program header offsets and counts."
    )]
    public ElfHeaderInfo ElfHeader { get; private set; } = new();

    [Seed(
        name: "Section Table",
        description: "Sections are the containers of the actual data of the ELF object. " +
                     "Each section has a type, flags (alloc/write/exec), virtual address, " +
                     "file offset and size. Non-allocated sections (symbol tables, debug, etc.) " +
                     "are not loaded into memory at runtime."
    )]
    public ElfSection[] Sections { get; private set; } = [];

    [Seed(
        name: "Program Headers (Segments)",
        description: "Program headers describe segments that the system loader maps into memory. " +
                     "Each segment has a type (LOAD, DYNAMIC, INTERP, etc.), virtual address, " +
                     "file/memory size and permissions (read/write/execute)."
    )]
    public ElfSegment[] Segments { get; private set; } = [];

    [Seed(
        name: "Symbol Table",
        description: "Symbol table entries (.symtab and .dynsym merged). Each symbol has a name, " +
                     "value (address), size, binding (local/global/weak), type (function/object) " +
                     "and section index. Undefined, absolute and common symbols are also reported."
    )]
    public ElfSymbolModel[] Symbols { get; private set; } = [];

    [Seed(
        name: "Dynamic Entries",
        description: "Dynamic table entries (.dynamic) for shared objects and executables. " +
                     "Lists needed libraries, PLT/GOT relocation info, hash table and other " +
                     "dynamic linker controls."
    )]
    public ElfDynamicEntry[] DynamicEntries { get; private set; } = [];
    

    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var dump = new ElfDumpManager(filePath);
        
        ElfHeader = new ElfHeaderInfo
        {
            Class = dump.Is64Bit ? ElfClassType.Elf64 : ElfClassType.Elf32,
            Data = dump.IsLittleEndian ? EndianType.LittleEndian : EndianType.BigEndian,
            Type = dump.ImageDetails.Type,
            Machine = dump.ImageDetails.Machine,
            EntryPoint = dump.ImageDetails.EntryPoint,
            SectionHeaderCount = dump.ImageDetails.SectionHeaderCount,
            SectionHeaderOffset = dump.ImageDetails.SectionHeaderOffset,
            ProgramHeaderCount = dump.ImageDetails.ProgramHeaderCount,
            ProgramHeaderOffset = dump.ImageDetails.ProgramHeaderOffset,
        };

        Sections = dump.Sections;
        Segments = dump.Segments;
        Symbols = dump.Symbols;
        DynamicEntries = dump.DynamicEntries;
    });

    public string Name => "Dump Executable Linkable";
}

/// <summary> Simplified ELF header containment. </summary>
public class ElfHeaderInfo
{
    public ElfClassType Class { get; set; }
    public EndianType Data { get; set; }
    public ElfType Type { get; set; }
    public ElfMachine Machine { get; set; }
    public ulong EntryPoint { get; set; }
    public ulong SectionHeaderOffset { get; set; }
    public ushort SectionHeaderCount { get; set; }
    public ulong ProgramHeaderOffset { get; set; }
    public ushort ProgramHeaderCount { get; set; }
}

public enum ElfClassType
{
    Elf32,
    Elf64,
}

/// <summary> Endianness for display. </summary>
public enum EndianType
{
    LittleEndian,
    BigEndian,
}