// CoffeeLake (C) 2026-*
// 
// The Header.cs represents .a.out files program header structure
// which was declared by the Berkely 4 standard. Usually this (not extended) format
// uses in Minix, and other "popular" unix like operating systems
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
using System.Runtime.InteropServices;

namespace SunFlower.Unix.Format;
/// <summary>
/// Defines available 
/// </summary>
public enum MagicWord : ushort
{
    /// <summary>
    /// Old impure format. .data and .text sections loads
    /// into the writable virtual memory (-rw/xrw)
    ///
    /// If the magic Number is OMAGIC (0407), the text segment starts at location 0
    /// in the address space and is not to be write-protected or shared,
    /// so the data segment is immediately contiguous with the text segment.
    ///
    /// This is rarely used.
    /// </summary>
    OMAGIC = 0x0107, //0407, Old magic
    MINIX_MAGIC = 0x0301, // 0403 Old magic too
    /// <summary>
    /// Program text loads into read-only virtual memory
    /// If the magic Number is NMAGIC (0410), the data segment begins at the first 0 mod 1024 byte boundary
    /// following the text segment, and the program cannot write on the text segment.
    /// Just as in the OMAGIC file, however, the data follows immediately after the text segment
    /// in the a.out file.
    ///
    /// This is the format used by the assembler
    /// </summary>
    NMAGIC = 0x0108, // 0410, New magic

    /// <summary>
    /// If the magic Number is ZMAGIC (0413), the data segment begins at the first 0 mod 1024 byte boundary
    /// following the text segment, the size of the text segment is a whole Number
    /// of 1024 byte blocks, and the program cannot write on text segment.
    /// If other processes are executing the same file, they will share the text segment.
    /// </summary>
    ZMAGIC = 0x010B, // Zeroed Magic?

    /// <summary>
    /// Demand load format. Locations 0-1023 are unmapped.
    /// 
    /// If the magic Number is XMAGIC (0414), the first two pages of the address space are unmapped
    /// to trap references through zero pointers, and the text segment starts at address 0x400,
    /// but otherwise the format is as for ZMAGIC.
    /// </summary>
    XMAGIC = 0x010C, // 0414, why X?
    /// <summary>
    /// Demand load format. Locations 0-4095 are unmapped.
    /// 
    /// This indicates a demand-page executable with a header in the text
    /// The first page is unmapped to help trap NULL pointer references 
    /// </summary>
    QMAGIC = 0x00CC // 0314 Quad Magic...? 
}

[Flags]
public enum Flags : byte
{
    Unmapped = 0x01,
    PageAligned = 0x02,
    NewSymbolTable = 0x04,
    Image = 0x08,
    Executable = 0x10,
    SeparateIpD = 0x20,
    PureText = 0x40,
    TextOverlay = 0x80
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Relocation 
{
    public long VirtualAddress;
    /// <summary>
    /// Internal object Number or extern symbol name. 
    /// If given relocation type is an internal reference -> 
    /// look the <see cref="SymbolIndexInternalRelocation"/> values.
    /// </summary>
    public ushort SymbolIndex;
    public ushort Type;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Symbol
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public char[] Name; 
    public ulong Value;
    public byte StorageClass;
    public char Number;
    public short Type; // language base | ntype 
}

public enum SymbolIndexInternalRelocation : short
{
    Absolute = -1,
    Text = -2,
    Data = -3,
    Bss = -4
}
/* High bits of storage class. */

public enum LowBitsStorageClass : byte
{
    Undefined = 00,
    Absolute = 01,
    Text = 02,
    Data = 03,
    Bss = 04,
    Common = 05,
    SectionMask = 07,
}

[Flags]
public enum HighBitsStorageClass : byte
{
    Class = 0xF8, // 0o370
    Null = 00,
    External = 0020,
    Static = 0030
}
/// <summary>
/// Supported (declared) CPU types for target executable 
/// </summary>
public enum AOutMachine : byte
{
    /// <summary>
    /// Unknown
    /// </summary>
    None = 0x00,
    /// <summary>
    /// Intel 8086/8088
    /// </summary>
    I8086 = 0x04,
    /// <summary>
    /// Motorola 68000
    /// </summary>
    M68K = 0x0B,
    /// <summary>
    /// National Semiconductor 16_32
    /// </summary>
    NationalSemiconductor = 0x0C,
    /// <summary>
    /// Intel 80386
    /// </summary>
    I386 = 0x10,
    /// <summary>
    /// Sun Sparc
    /// </summary>
    Sparc = 0x17,
};
/// <summary>
/// In the header the sizes of each section are given in bytes
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Header
{
    /// <summary>
    /// Main word or the header -> defines how the operating system will load
    /// given file image
    /// </summary>
    [MarshalAs(UnmanagedType.U2)]
    public MagicWord MagicWord;
    /// <summary>
    /// Flags field based on the Minix 3x source
    /// </summary>
    [MarshalAs(UnmanagedType.U2)]
    public ushort Flags;
    
    [MarshalAs(UnmanagedType.U1)]
    public AOutMachine CpuId;
    /// <summary>
    /// Length of the header
    /// </summary>
    public byte HeaderLength;
    /// <summary>
    /// Maybe zero?
    /// </summary>
    public byte Unused;
    /// <summary>
    /// Unused. Stays here for a historical context
    /// </summary>
    public ushort Version;
    /// <summary>
    /// Size of program text
    /// </summary>
    public uint TextSize;
    /// <summary>
    /// Size of the data
    /// </summary>
    public uint DataSize;
    /// <summary>
    /// Size of not initialized data 
    /// </summary>
    public uint Bss;
    /// <summary>
    /// Symbol table offset
    /// </summary>
    public uint Symbols;
    /// <summary>
    /// Contains the address in memory of the entry point 
    /// of the program after the kernel has loaded it; 
    /// the kernel starts the execution of the program 
    /// from the machine instruction at this address.
    /// </summary>
    public uint Entry;
    /// <summary>
    /// Text Relocations size
    /// </summary>
    public uint TextRelocSize;
    /// <summary>
    /// Data relocations size
    /// </summary>
    public uint DataRelocSize;
}