// CoffeeLake (C) 2026-*
//
// The ElfHeader.cs represents ELF32/ELF64 data types:
//   - ELF identification (e_ident)
//   - ELF32 / ELF64 file header (Ehdr)
//   - ELF32 / ELF64 section header (Shdr)
//   - ELF32 / ELF64 program header (Phdr)
//   - ELF32 / ELF64 symbol table entry (Sym)
//   - ELF32 / ELF64 relocation entries (Rel / Rela)
//   - ELF32 / ELF64 dynamic table entry (Dyn)
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Runtime.InteropServices;

namespace SunFlower.Elf.Headers;

/// <summary>
/// ELF identification block (first 16 bytes of every ELF file).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ElfIdent
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Magic;        // 0x7f, 'E', 'L', 'F'
    public ElfClass Class;      // EI_CLASS: 1 = 32-bit, 2 = 64-bit
    public ElfData Data;        // EI_DATA: 1 = little-endian, 2 = big-endian
    public byte Version;        // EI_VERSION: must be 1 (EV_CURRENT)
    public byte OsAbi;          // EI_OSABI
    public byte AbiVersion;     // EI_ABIVERSION
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
    public byte[] Pad;          // EI_PAD: padding
}

/// <summary> ELF32 file header. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32FileHeader
{
    public ElfIdent Ident;
    public ElfType Type;        // e_type
    public ElfMachine Machine;  // e_machine
    public ElfVersion Version;  // e_version
    public uint Entry;          // e_entry virtual address
    public uint ProgramHeaderOffset; // e_phoff
    public uint SectionHeaderOffset; // e_shoff
    public uint Flags;          // e_flags
    public ushort EhSize;       // e_ehsize
    public ushort PhEntrySize;  // e_phentsize
    public ushort PhNum;        // e_phnum
    public ushort ShEntrySize;  // e_shentsize
    public ushort ShNum;        // e_shnum
    public ushort ShStrIndex;   // e_shstrndx
}

/// <summary> ELF64 file header. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64FileHeader
{
    public ElfIdent Ident;
    public ElfType Type;        // e_type
    public ElfMachine Machine;  // e_machine
    public ElfVersion Version;  // e_version
    public ulong Entry;         // e_entry virtual address
    public ulong ProgramHeaderOffset; // e_phoff
    public ulong SectionHeaderOffset; // e_shoff
    public uint Flags;          // e_flags
    public ushort EhSize;       // e_ehsize
    public ushort PhEntrySize;  // e_phentsize
    public ushort PhNum;        // e_phnum
    public ushort ShEntrySize;  // e_shentsize
    public ushort ShNum;        // e_shnum
    public ushort ShStrIndex;   // e_shstrndx
}

/// <summary> ELF32 section header. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32SectionHeader
{
    public uint Name;           // sh_name offset into section name string table
    public ElfSectionType Type; // sh_type
    public ElfSectionFlags Flags; // sh_flags
    public uint Address;        // sh_addr: virtual address
    public uint Offset;         // sh_offset: file offset
    public uint Size;           // sh_size
    public uint Link;           // sh_link
    public uint Info;           // sh_info
    public uint AddrAlign;      // sh_addralign
    public uint EntSize;        // sh_entsize
}

/// <summary> ELF64 section header. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64SectionHeader
{
    public uint Name;           // sh_name
    public ElfSectionType Type; // sh_type
    public ElfSectionFlags Flags; // sh_flags
    public ulong Address;       // sh_addr
    public ulong Offset;        // sh_offset
    public ulong Size;          // sh_size
    public uint Link;           // sh_link
    public uint Info;           // sh_info
    public ulong AddrAlign;     // sh_addralign
    public ulong EntSize;       // sh_entsize
}

/// <summary> ELF32 program header (segment). </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32ProgramHeader
{
    public ElfSegmentType Type;   // p_type
    public uint Offset;           // p_offset: file offset
    public uint VirtualAddress;   // p_vaddr
    public uint PhysicalAddress;  // p_paddr
    public uint FileSize;         // p_filesz
    public uint MemorySize;       // p_memsz
    public ElfSegmentFlags Flags; // p_flags
    public uint Align;            // p_align
}

/// <summary> ELF64 program header (segment). </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64ProgramHeader
{
    public ElfSegmentType Type;   // p_type
    public ElfSegmentFlags Flags; // p_flags
    public ulong Offset;          // p_offset
    public ulong VirtualAddress;  // p_vaddr
    public ulong PhysicalAddress; // p_paddr
    public ulong FileSize;        // p_filesz
    public ulong MemorySize;      // p_memsz
    public ulong Align;           // p_align
}

/// <summary> ELF32 symbol table entry. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32Symbol
{
    public uint Name;           // st_name: offset into string table
    public uint Value;          // st_value
    public uint Size;           // st_size
    public byte Info;           // st_info: bind (hi 4) | type (lo 4)
    public byte Other;          // st_other: visibility
    public ushort SectionIndex; // st_shndx
}

/// <summary> ELF64 symbol table entry. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64Symbol
{
    public uint Name;           // st_name
    public byte Info;           // st_info
    public byte Other;          // st_other
    public ushort SectionIndex; // st_shndx
    public ulong Value;         // st_value
    public ulong Size;          // st_size
}

/// <summary> ELF32 relocation entry without addend. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32Relocation
{
    public uint Offset;  // r_offset
    public uint Info;    // r_info
}

/// <summary> ELF32 relocation entry with addend. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32RelocationAddend
{
    public uint Offset;  // r_offset
    public uint Info;    // r_info
    public int Addend;   // r_addend
}

/// <summary> ELF64 relocation entry without addend. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64Relocation
{
    public ulong Offset; // r_offset
    public ulong Info;   // r_info
}

/// <summary> ELF64 relocation entry with addend. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64RelocationAddend
{
    public ulong Offset; // r_offset
    public ulong Info;   // r_info
    public long Addend;  // r_addend
}

/// <summary> ELF32 dynamic table entry. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32Dynamic
{
    public ElfDynamicTag Tag;   // d_tag
    public uint Val;            // d_val / d_ptr
}

/// <summary> ELF64 dynamic table entry. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf64Dynamic
{
    public ElfDynamicTag Tag;   // d_tag
    public ulong Val;           // d_val / d_ptr
}