using SunFlower.Elf.Headers;

namespace SunFlower.Elf.Models;

/// <summary>
/// Model representation of an ELF symbol table entry,
/// suitable for display in the SunFlower client.
/// </summary>
public class ElfSymbolModel
{
    public string Name { get; set; } = string.Empty;
    public ulong Value { get; set; }
    public ulong Size { get; set; }
    public ElfSymbolBind Bind { get; set; }
    public ElfSymbolType Type { get; set; }
    public ElfSymbolVisibility Visibility { get; set; }
    public ushort SectionIndex { get; set; }

    /// <summary> Human readable section name (or special index text). </summary>
    public string SectionName { get; set; } = string.Empty;

    public bool IsFunction => Type == ElfSymbolType.Func;
    public bool IsObject => Type == ElfSymbolType.Object;
    public bool IsUndefined => SectionIndex == (ushort)ElfSectionIndex.Undef;
    public bool IsAbsolute => SectionIndex == (ushort)ElfSectionIndex.Abs;
    public bool IsCommon => SectionIndex == (ushort)ElfSectionIndex.Common;

    public override string ToString() =>
        $"{Name} @ 0x{Value:X} size=0x{Size:X} bind={Bind} type={Type}";
}

/// <summary>
/// Model representation of an ELF dynamic table entry.
/// </summary>
public class ElfDynamicEntry
{
    public ElfDynamicTag Tag { get; set; }
    public ulong Val { get; set; }

    public override string ToString() => $"{Tag} = 0x{Val:X}";
}