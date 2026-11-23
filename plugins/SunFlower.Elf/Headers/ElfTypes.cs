// CoffeeLake (C) 2026-*
//
// The ElfTypes.cs represents ELF constants, enums and flags
// shared between ELF32 and ELF64 parsers.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

namespace SunFlower.Elf.Headers;

/// <summary> ELF magic number: \x7fELF </summary>
public static class ElfMagic
{
    public const uint Magic = 0x464c457f; // "\x7fELF" in little-endian
}

/// <summary> e_ident indices </summary>
public static class ElfIdentConst
{
    public const int EI_MAG0 = 0;
    public const int EI_MAG1 = 1;
    public const int EI_MAG2 = 2;
    public const int EI_MAG3 = 3;
    public const int EI_CLASS = 4;
    public const int EI_DATA = 5;
    public const int EI_VERSION = 6;
    public const int EI_OSABI = 7;
    public const int EI_ABIVERSION = 8;
    public const int EI_PAD = 9;
    public const int EI_NIDENT = 16;
}

/// <summary> e_ident[EI_CLASS] values </summary>
public enum ElfClass : byte
{
    None = 0,
    Elf32 = 1,
    Elf64 = 2,
}

/// <summary> e_ident[EI_DATA] values </summary>
public enum ElfData : byte
{
    None = 0,
    LittleEndian = 1,
    BigEndian = 2,
}

/// <summary> e_type values </summary>
public enum ElfType : ushort
{
    None = 0,
    Rel = 1,      // Relocatable file
    Exec = 2,     // Executable file
    Dyn = 3,      // Shared object file
    Core = 4,     // Core file
    LoNum = 0xff00,
    HiNum = 0xffff,
}

/// <summary> e_machine values (subset relevant to x86) </summary>
public enum ElfMachine : ushort
{
    None = 0,
    M32 = 1,
    Sparc = 2,
    I386 = 3,
    M68k = 4,
    M88k = 5,
    I860 = 6,
    Mips = 7,
    S370 = 8,
    MipsRs3000Le = 9,
    Parisc = 15,
    Vpp500 = 17,
    Sparc32Plus = 18,
    I960 = 19,
    Ppc = 20,
    Ppc64 = 21,
    S390 = 22,
    V800 = 23,
    Fr20 = 24,
    Rh32 = 25,
    Rce = 26,
    Arm = 40,
    Alpha = 41,
    Sh = 42,
    Sparcv9 = 43,
    H8300 = 44,
    I686 = 45,
    PdP10 = 46,
    PdP11 = 47,
    X8664 = 62,   // AMD x86-64
    Ppc64Le = 243,
}

/// <summary> e_version values </summary>
public enum ElfVersion : uint
{
    None = 0,
    Current = 1,
}

/// <summary> Section header sh_type values </summary>
public enum ElfSectionType : uint
{
    Null = 0,
    Progbits = 1,
    Symtab = 2,
    Strtab = 3,
    Rela = 4,
    Hash = 5,
    Dynamic = 6,
    Note = 7,
    Nobits = 8,
    Rel = 9,
    Shlib = 10,
    Dynsym = 11,
    InitArray = 14,
    FiniArray = 15,
    PreInitArray = 16,
    Group = 17,
    SymtabShndx = 18,
    Num = 19,
    LoOs = 0x60000000,
    HiOs = 0x6fffffff,
    LoProc = 0x70000000,
    HiProc = 0x7fffffff,
    LoUser = 0x80000000,
    HiUser = 0xffffffff,
}

/// <summary> Section header sh_flags values </summary>
[Flags]
public enum ElfSectionFlags : uint
{
    None = 0,
    Write = 0x1,
    Alloc = 0x2,
    ExecInstr = 0x4,
    Merge = 0x10,
    Strings = 0x20,
    InfoLink = 0x40,
    LinkOrder = 0x80,
    OsNonconforming = 0x100,
    Group = 0x200,
    Tls = 0x400,
    Compressed = 0x800,
    Ordered = 0x4000,
    Exclude = 0x8000,
    MaskOs = 0x0ff00000,
    MaskProc = 0xf0000000,
}

/// <summary> Program header p_type values </summary>
public enum ElfSegmentType : uint
{
    Null = 0,
    Load = 1,
    Dynamic = 2,
    Interp = 3,
    Note = 4,
    Shlib = 5,
    Phdr = 6,
    Tls = 7,
    Num = 8,
    LoOs = 0x60000000,
    HiOs = 0x6fffffff,
    LoProc = 0x70000000,
    HiProc = 0x7fffffff,
}

/// <summary> Program header p_flags values </summary>
[Flags]
public enum ElfSegmentFlags : uint
{
    None = 0,
    Execute = 0x1,
    Write = 0x2,
    Read = 0x4,
}

/// <summary> Dynamic entry d_tag values </summary>
public enum ElfDynamicTag : long
{
    Null = 0,
    Needed = 1,
    PltRelSz = 2,
    PltGot = 3,
    Hash = 4,
    StrTab = 5,
    SymTab = 6,
    Rela = 7,
    RelasZ = 8,
    Relent = 9,
    RelaEnt = 10,
    Versym = 13,
    RelCount = 0x6ffffffd,
    Flags = 0x6ffffff0,
    /// <summary> ELF64-only: address of PLT relocations </summary>
    JmpRel = 0x17,
    /// <summary> ELF64-only: PLT relocation entries </summary>
    PltRel = 0x11,
}

/// <summary> st_info high 4 bits </summary>
public enum ElfSymbolBind : byte
{
    Local = 0,
    Global = 1,
    Weak = 2,
    Num = 3,
    LoOs = 10,
    HiOs = 12,
    LoProc = 13,
    HiProc = 15,
}

/// <summary> st_info low 4 bits </summary>
public enum ElfSymbolType : byte
{
    NotType = 0,
    Object = 1,
    Func = 2,
    Section = 3,
    File = 4,
    Common = 5,
    Tls = 6,
    Num = 7,
    LoOs = 10,
    HiOs = 12,
    LoProc = 13,
    HiProc = 15,
}

/// <summary> Symbol visibility: st_other low bits </summary>
public enum ElfSymbolVisibility : byte
{
    Default = 0,
    Internal = 1,
    Hidden = 2,
    Protected = 3,
}

/// <summary> Special section index values </summary>
public enum ElfSectionIndex : ushort
{
    Undef = 0,
    LoReserve = 0xff00,
    After = 0xff00,
    Before = 0xff01,
    Amd64_LCommon = 0xff02,
    Abs = 0xfff1,
    Common = 0xfff2,
    XIndex = 0xffff,
    HiReserve = 0xffff,
}

/// <summary> Relocation types for i386 </summary>
public enum ElfRelocTypeI386 : int
{
    None = 0,
    GOT32 = 1,
    PLT32 = 2,
    Copy = 3,
    GLoblDat = 4,
    JMPSlot = 7,
    Relative = 8,
    GOTPC = 10,
}

/// <summary> Relocation types for x86-64 </summary>
public enum ElfRelocTypeX8664 : int
{
    None = 0,
    GOTPCRel = 1,
    Rel32 = 4,
    Copy = 5,
    GlobDat = 7,
    JmpSlot = 8,
    Relative = 9,
    Gotoff = 12,
    GotPcRel = 13,
    Off64 = 27,
}