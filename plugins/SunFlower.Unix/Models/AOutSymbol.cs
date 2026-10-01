using SunFlower.Unix.Format;

namespace SunFlower.Unix.Models;

/// <summary>
/// A decoded symbol table entry ready for presentation:
/// combines the raw <see cref="Nlist"/> record with its resolved name
/// (taken from the string table) and human-readable type information.
/// </summary>
public sealed class AOutSymbol
{
    private readonly Nlist _raw;

    public AOutSymbol(Nlist raw, string name)
    {
        _raw = raw;
        Name = name;
    }

    /// <summary>Resolved symbol name (C-string from the string table).</summary>
    public string Name { get; }

    /// <summary>Raw `n_type` byte.</summary>
    public byte Type => _raw.Type;

    /// <summary>True if the external bit (`N_EXT`) is set -> symbol is visible from other binaries.</summary>
    public bool IsExternal => (_raw.Type & NSymbolTypeBits.External) != 0;

    /// <summary>True if the symbol is a debugger (stab) entry.</summary>
    public bool IsDebug => (_raw.Type & NSymbolTypeBits.StabMask) != 0;

    /// <summary>The section/kind of the symbol (Undefined/Absolute/Text/Data/Bss/FileName).</summary>
    public string SectionKind
    {
        get
        {
            if (IsDebug)
                return $"stab(0x{_raw.Type & NSymbolTypeBits.StabMask:X2})";

            return (_raw.Type & NSymbolTypeBits.TypeMask) switch
            {
                NSymbolTypeBits.Undefined => "UNDEF",
                NSymbolTypeBits.Absolute => "ABS",
                NSymbolTypeBits.Text => "TEXT",
                NSymbolTypeBits.Data => "DATA",
                NSymbolTypeBits.Bss => "BSS",
                NSymbolTypeBits.FileName => "FILE",
                _ => $"? (0x{_raw.Type & NSymbolTypeBits.TypeMask:X2})"
            };
        }
    }

    /// <summary>Decoded auxiliary type (Func/Object) from the low nibble of `n_other`.</summary>
    public string AuxKind
    {
        get
        {
            return (_raw.Other & (byte)AuxType.Mask) switch
            {
                (byte)AuxType.Func => "func",
                (byte)AuxType.Object => "object",
                _ => "?"
            };
        }
    }

    /// <summary>Raw `n_desc` (debugger-specific).</summary>
    public short Desc => _raw.Desc;

    /// <summary>Raw `n_value`.</summary>
    public uint Value => _raw.Value;

    /// <summary>Offset of the name in the string table.</summary>
    public uint Strx => _raw.Strx;
}