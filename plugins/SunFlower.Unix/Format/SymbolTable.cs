using System.Runtime.InteropServices;

namespace SunFlower.Unix.Format;

/// <summary>
/// `n_type` sub-fields (from a.out.h / nlist.h of minix 2x).
/// The byte is split by bitmasks:
///  - bits 0x1e (`N_TYPE`) - the section a symbol refers to;
///  - bit  0x01 (`N_EXT`)  - whether the symbol is external (global);
///  - bits 0xe0 (`N_STAB`) - debugger (stab) information.
/// </summary>
public static class NSymbolTypeBits
{
    /// <summary>An undefined symbol. The link editor must locate an external symbol with the same name.</summary>
    public const byte Undefined = 0x00;
    /// <summary>An absolute symbol; the link editor does not update it.</summary>
    public const byte Absolute = 0x02;
    /// <summary>A text symbol; the value is a text address.</summary>
    public const byte Text = 0x04;
    /// <summary>A data symbol; the value is a data address.</summary>
    public const byte Data = 0x06;
    /// <summary>A bss symbol; has no offset in the binary file.</summary>
    public const byte Bss = 0x08;
    /// <summary>A file name symbol, inserted by the link editor when merging binaries.</summary>
    public const byte FileName = 0x1f;

    /// <summary>Mask for all the "interest to link editor" type bits.</summary>
    public const byte TypeMask = 0x1e;
    /// <summary>External bit. If set, the symbol is visible from other binary files.</summary>
    public const byte External = 0x01;
    /// <summary>Mask for debugger information (stab entries).</summary>
    public const byte StabMask = 0xe0;
}

/// <summary>
/// The `n_other` low 4 bits hold an auxiliary type that describes the
/// nature of the symbol independent of its segment location.
/// </summary>
public enum AuxType : byte
{
    None = 0x00,
    /// <summary>Associates the symbol with a callable function.</summary>
    Func = 0x01,
    /// <summary>Associates the symbol with data.</summary>
    Object = 0x02,
    Mask = 0x0f
}

/// <summary>
/// A single `struct nlist` record (12 bytes on 32-bit targets) from the symbol table.
/// The name is *not* stored inline - `Strx` is a byte offset into the string table.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Nlist
{
    /// <summary>
    /// Byte offset into the string table for the name of this symbol.
    /// A value of 0 typically means "no name".
    /// </summary>
    public uint Strx;

    /// <summary>
    /// Raw `n_type` byte. Split by <see cref="NSymbolTypeBits.TypeMask"/>,
    /// <see cref="NSymbolTypeBits.External"/> and <see cref="NSymbolTypeBits.StabMask"/>.
    /// </summary>
    public byte Type;

    /// <summary>
    /// Low 4 bits: <see cref="AuxType"/> (Func/Object). Passed untouched by the link editor.
    /// </summary>
    public byte Other;

    /// <summary>
    /// Reserved for use by debuggers; passed untouched by the link editor.
    /// Holds the stab type when the symbol is a debugger entry.
    /// </summary>
    public short Desc;

    /// <summary>
    /// The value of the symbol. For text/data/bss symbols this is an address;
    /// for debugger symbols the value may be arbitrary.
    /// </summary>
    public uint Value;
}