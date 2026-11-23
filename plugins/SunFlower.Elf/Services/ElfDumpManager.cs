using System.Text;
using SunFlower.Elf.Headers;
using SunFlower.Elf.Models;

namespace SunFlower.Elf.Services;

///
/// CoffeeLake 2026-*
/// This code is part of the SunFlower.Elf plugin for dumping
/// ELF32/ELF64 objects and executables.
///
/// Licensed under MIT
///

/// <summary>
/// Orchestrator for the ELF32/ELF64 format.
/// The whole image is parsed inside the constructor: the ELF header is read
/// and validated, then section/program headers, symbol tables and dynamic
/// entries are delegated to private helpers.
///
/// All multi-byte integers are read through <see cref="ElfEndianReader"/> so
/// both little-endian and big-endian ELF images are supported.
/// </summary>
public class ElfDumpManager
{
    /// <summary> Parsed ELF file header (32 bit). </summary>
    public Elf32FileHeader Ehdr32 { get; private set; }

    /// <summary> Parsed ELF file header (64 bit). </summary>
    public Elf64FileHeader Ehdr64 { get; private set; }

    /// <summary> True when the image is ELF64, false for ELF32. </summary>
    public bool Is64Bit { get; private set; }

    /// <summary> True when the image uses little-endian byte order. </summary>
    public bool IsLittleEndian { get; private set; }

    /// <summary> Common view of the most important image details. </summary>
    public ElfImageDetails ImageDetails { get; } = new();

    /// <summary> Decoded section headers with resolved names. </summary>
    public ElfSection[] Sections { get; private set; } = [];

    /// <summary> Decoded program headers (segments). </summary>
    public ElfSegment[] Segments { get; private set; } = [];

    /// <summary> Decoded symbol table entries (.symtab + .dynsym merged). </summary>
    public ElfSymbolModel[] Symbols { get; private set; } = [];

    /// <summary> Decoded dynamic table entries (.dynamic). </summary>
    public ElfDynamicEntry[] DynamicEntries { get; private set; } = [];

    /// <summary> Raw bytes of .strtab (used by .symtab). </summary>
    private byte[] _symtabStringTable = [];

    /// <summary> Raw bytes of .dynstr (used by .dynsym). </summary>
    private byte[] _dynsymStringTable = [];

    public ElfDumpManager(string path)
    {
        ImageDetails.FilePath = path;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

        ImageDetails.FileLength = stream.Length;

        var identBytes = new byte[16];
        stream.ReadExactly(identBytes, 0, 16);

        if (identBytes[0] != 0x7f || identBytes[1] != (byte)'E' ||
            identBytes[2] != (byte)'L' || identBytes[3] != (byte)'F')
            throw new NotSupportedException("File is not an ELF object (bad magic number)");

        var elfClass = (ElfClass)identBytes[4];
        var elfData = (ElfData)identBytes[5];

        Is64Bit = elfClass == ElfClass.Elf64;
        IsLittleEndian = elfData == ElfData.LittleEndian;
        ImageDetails.Is64Bit = Is64Bit;
        ImageDetails.IsLittleEndian = IsLittleEndian;

        stream.Position = 0;
        using var reader = new ElfEndianReader(stream, !IsLittleEndian);

        if (Is64Bit)
        {
            var ehdr = ReadElf64FileHeader(reader);
            Ehdr64 = ehdr;

            ImageDetails.Type = ehdr.Type;
            ImageDetails.Machine = ehdr.Machine;
            ImageDetails.EntryPoint = ehdr.Entry;
            ImageDetails.SectionHeaderOffset = ehdr.SectionHeaderOffset;
            ImageDetails.ProgramHeaderOffset = ehdr.ProgramHeaderOffset;
            ImageDetails.SectionHeaderEntrySize = ehdr.ShEntrySize;
            ImageDetails.SectionHeaderCount = ehdr.ShNum;
            ImageDetails.SectionNameStringTableIndex = ehdr.ShStrIndex;
            ImageDetails.ProgramHeaderEntrySize = ehdr.PhEntrySize;
            ImageDetails.ProgramHeaderCount = ehdr.PhNum;
        }
        else
        {
            var ehdr = ReadElf32FileHeader(reader);
            Ehdr32 = ehdr;

            ImageDetails.Type = ehdr.Type;
            ImageDetails.Machine = ehdr.Machine;
            ImageDetails.EntryPoint = ehdr.Entry;
            ImageDetails.SectionHeaderOffset = ehdr.SectionHeaderOffset;
            ImageDetails.ProgramHeaderOffset = ehdr.ProgramHeaderOffset;
            ImageDetails.SectionHeaderEntrySize = ehdr.ShEntrySize;
            ImageDetails.SectionHeaderCount = ehdr.ShNum;
            ImageDetails.SectionNameStringTableIndex = ehdr.ShStrIndex;
            ImageDetails.ProgramHeaderEntrySize = ehdr.PhEntrySize;
            ImageDetails.ProgramHeaderCount = ehdr.PhNum;
        }

        Sections = ReadSectionHeaders(reader);
        Segments = ReadProgramHeaders(reader);
        var sectionNameStrings = ReadSectionNameStringTable(reader);

        for (var i = 0; i < Sections.Length; i++)
        {
            var nameOffset = ReadSectionNameOffset(reader, i);
            Sections[i].Name = GetString(sectionNameStrings, nameOffset);
        }

        Symbols = ReadSymbolTables(reader);
        DynamicEntries = ReadDynamicTable(reader);

        ImageDetails.Symbols = Symbols;
        ImageDetails.Sections = Sections;
        ImageDetails.Segments = Segments;
        ImageDetails.DynamicEntries = DynamicEntries;
    }
    
    private static Elf32FileHeader ReadElf32FileHeader(ElfEndianReader r)
    {
        var ident = ReadElfIdent(r);
        return new Elf32FileHeader
        {
            Ident = ident,
            Type = (ElfType)r.ReadUInt16(),
            Machine = (ElfMachine)r.ReadUInt16(),
            Version = (ElfVersion)r.ReadUInt32(),
            Entry = r.ReadUInt32(),
            ProgramHeaderOffset = r.ReadUInt32(),
            SectionHeaderOffset = r.ReadUInt32(),
            Flags = r.ReadUInt32(),
            EhSize = r.ReadUInt16(),
            PhEntrySize = r.ReadUInt16(),
            PhNum = r.ReadUInt16(),
            ShEntrySize = r.ReadUInt16(),
            ShNum = r.ReadUInt16(),
            ShStrIndex = r.ReadUInt16(),
        };
    }

    private static Elf64FileHeader ReadElf64FileHeader(ElfEndianReader r)
    {
        var ident = ReadElfIdent(r);
        return new Elf64FileHeader
        {
            Ident = ident,
            Type = (ElfType)r.ReadUInt16(),
            Machine = (ElfMachine)r.ReadUInt16(),
            Version = (ElfVersion)r.ReadUInt32(),
            Entry = r.ReadUInt64(),
            ProgramHeaderOffset = r.ReadUInt64(),
            SectionHeaderOffset = r.ReadUInt64(),
            Flags = r.ReadUInt32(),
            EhSize = r.ReadUInt16(),
            PhEntrySize = r.ReadUInt16(),
            PhNum = r.ReadUInt16(),
            ShEntrySize = r.ReadUInt16(),
            ShNum = r.ReadUInt16(),
            ShStrIndex = r.ReadUInt16(),
        };
    }

    private static ElfIdent ReadElfIdent(ElfEndianReader r)
    {
        var magic = r.ReadBytes(4);
        return new ElfIdent
        {
            Magic = magic,
            Class = (ElfClass)r.ReadByte(),
            Data = (ElfData)r.ReadByte(),
            Version = r.ReadByte(),
            OsAbi = r.ReadByte(),
            AbiVersion = r.ReadByte(),
            Pad = r.ReadBytes(7),
        };
    }
    
    private ElfSection[] ReadSectionHeaders(ElfEndianReader reader)
    {
        var count = ImageDetails.SectionHeaderCount;
        var entrySize = ImageDetails.SectionHeaderEntrySize;
        var offset = ImageDetails.SectionHeaderOffset;

        if (count == 0 || offset == 0)
            return [];

        var sections = new ElfSection[count];

        for (var i = 0; i < count; i++)
        {
            Seek(reader, offset + (ulong)i * entrySize);

            ElfSection section;
            if (Is64Bit)
            {
                section = ReadElf64SectionHeader(reader);
            }
            else
            {
                section = ReadElf32SectionHeader(reader);
            }

            sections[i] = section;
        }

        return sections;
    }

    private static ElfSection ReadElf32SectionHeader(ElfEndianReader r)
    {
        r.ReadUInt32(); // sh_name
        return new ElfSection
        {
            Type      = (ElfSectionType)r.ReadUInt32(),    // sh_type
            Flags     = (ElfSectionFlags)r.ReadUInt32(),   // sh_flags
            Address   = r.ReadUInt32(),                    // sh_addr
            Offset    = r.ReadUInt32(),                    // sh_offset
            Size      = r.ReadUInt32(),                    // sh_size
            Link      = r.ReadUInt32(),                    // sh_link
            Info      = r.ReadUInt32(),                    // sh_info
            AddrAlign = r.ReadUInt32(),                    // sh_addralign
            EntSize   = r.ReadUInt32(),                    // sh_entsize
        };
    }

    private static ElfSection ReadElf64SectionHeader(ElfEndianReader r)
    {
        r.ReadUInt32(); // sh_name

        return new ElfSection
        {
            Type      = (ElfSectionType)r.ReadUInt32(),           // sh_type (4)
            Flags     = (ElfSectionFlags)(uint)r.ReadUInt64(),    // sh_flags safe trim (8!)
            Address   = r.ReadUInt64(),                           // sh_addr
            Offset    = r.ReadUInt64(),                           // sh_offset
            Size      = r.ReadUInt64(),                           // sh_size
            Link      = r.ReadUInt32(),                           // sh_link
            Info      = r.ReadUInt32(),                           // sh_info
            AddrAlign = r.ReadUInt64(),                           // sh_addralign
            EntSize   = r.ReadUInt64(),                           // sh_entsize
        };
    }

    private uint ReadSectionNameOffset(ElfEndianReader reader, int index)
    {
        var entrySize = ImageDetails.SectionHeaderEntrySize;
        var offset = ImageDetails.SectionHeaderOffset + (ulong)index * entrySize;
        Seek(reader, offset);

        // sh_name is the first field of both
        return reader.ReadUInt32();
    }

    private byte[] ReadSectionNameStringTable(ElfEndianReader reader)
    {
        var strIdx = ImageDetails.SectionNameStringTableIndex;
        if (strIdx == 0 || strIdx >= Sections.Length)
            return [];

        var strSection = Sections[strIdx];
        if (strSection.Size == 0)
            return [];

        return ReadSectionBytes(reader, strSection);
    }

    private static string GetString(byte[] table, uint offset)
    {
        if (offset == 0 || offset >= table.Length)
            return string.Empty;

        var start = (int)offset;
        var end = start;
        while (end < table.Length && table[end] != 0)
            end++;

        return Encoding.ASCII.GetString(table, start, end - start);
    }

    private byte[] ReadSectionBytes(ElfEndianReader reader, ElfSection section)
    {
        if (section.Size == 0)
            return [];

        var data = new byte[section.Size];
        Seek(reader, section.Offset);
        var read = reader.Read(data, 0, data.Length);
        if (read < data.Length)
            Array.Resize(ref data, read);
        return data;
    }

    private static void Seek(ElfEndianReader reader, ulong position)
    {
        reader.BaseStream.Seek((long)position, SeekOrigin.Begin);
    }

    private ElfSegment[] ReadProgramHeaders(ElfEndianReader reader)
    {
        var count = ImageDetails.ProgramHeaderCount;
        var entrySize = ImageDetails.ProgramHeaderEntrySize;
        var offset = ImageDetails.ProgramHeaderOffset;

        if (count == 0 || offset == 0)
            return [];

        var segments = new ElfSegment[count];

        for (var i = 0; i < count; i++)
        {
            Seek(reader, offset + (ulong)i * entrySize);

            ElfSegment segment;
            if (Is64Bit)
            {
                segment = ReadElf64ProgramHeader(reader);
            }
            else
            {
                segment = ReadElf32ProgramHeader(reader);
            }

            segments[i] = segment;
        }

        return segments;
    }

    private static ElfSegment ReadElf32ProgramHeader(ElfEndianReader r)
    {
        return new ElfSegment
        {
            Type = (ElfSegmentType)r.ReadUInt32(),
            Offset = r.ReadUInt32(),
            VirtualAddress = r.ReadUInt32(),
            PhysicalAddress = r.ReadUInt32(),
            FileSize = r.ReadUInt32(),
            MemorySize = r.ReadUInt32(),
            Flags = (ElfSegmentFlags)r.ReadUInt32(),
            Align = r.ReadUInt32(),
        };
    }

    private static ElfSegment ReadElf64ProgramHeader(ElfEndianReader r)
    {
        return new ElfSegment
        {
            Type = (ElfSegmentType)r.ReadUInt32(),
            Flags = (ElfSegmentFlags)r.ReadUInt32(),
            Offset = r.ReadUInt64(),
            VirtualAddress = r.ReadUInt64(),
            PhysicalAddress = r.ReadUInt64(),
            FileSize = r.ReadUInt64(),
            MemorySize = r.ReadUInt64(),
            Align = r.ReadUInt64(),
        };
    }

    private ElfSymbolModel[] ReadSymbolTables(ElfEndianReader reader)
    {
        var allSymbols = new List<ElfSymbolModel>();

        foreach (var section in Sections)
        {
            if (section.Type != ElfSectionType.Symtab && section.Type != ElfSectionType.Dynsym)
                continue;

            byte[] stringTable;
            if (section.Type == ElfSectionType.Symtab)
                _symtabStringTable = stringTable = ReadStringTableByIndex(reader, section.Link);
            else
                _dynsymStringTable = stringTable = ReadStringTableByIndex(reader, section.Link);

            var entrySize = Is64Bit ? 24UL : 16UL;
            var count = (int)(section.Size / entrySize);

            if ((long)section.Offset + count * (long)entrySize > ImageDetails.FileLength)
                count = (int)((ImageDetails.FileLength - (long)section.Offset) / (long)entrySize);
            
            if (count <= 0) continue;

            Seek(reader, section.Offset);

            for (var i = 0; i < count; i++)
            {
                ElfSymbolModel sym;
                if (Is64Bit)
                {
                    sym = ReadElf64Symbol(reader, stringTable);
                }
                else
                {
                    sym = ReadElf32Symbol(reader, stringTable);
                }

                allSymbols.Add(sym);
            }
        }

        return allSymbols.ToArray();
    }

    private ElfSymbolModel ReadElf32Symbol(ElfEndianReader r, byte[] stringTable)
    {
        var nameOffset = r.ReadUInt32();
        var value = r.ReadUInt32();
        var size = r.ReadUInt32();
        var info = r.ReadByte();
        var other = r.ReadByte();
        var sectionIndex = r.ReadUInt16();

        var bind = (ElfSymbolBind)(info >> 4);
        var type = (ElfSymbolType)(info & 0x0f);
        var visibility = (ElfSymbolVisibility)(other & 0x03);

        return new ElfSymbolModel
        {
            Name = GetString(stringTable, nameOffset),
            Value = value,
            Size = size,
            Bind = bind,
            Type = type,
            Visibility = visibility,
            SectionIndex = sectionIndex,
            SectionName = ResolveSectionNameByIndex(Sections, sectionIndex),
        };
    }

    private ElfSymbolModel ReadElf64Symbol(ElfEndianReader r, byte[] stringTable)
    {
        var nameOffset = r.ReadUInt32();
        var info = r.ReadByte();
        var other = r.ReadByte();
        var sectionIndex = r.ReadUInt16();
        var value = r.ReadUInt64();
        var size = r.ReadUInt64();

        var bind = (ElfSymbolBind)(info >> 4);
        var type = (ElfSymbolType)(info & 0x0f);
        var visibility = (ElfSymbolVisibility)(other & 0x03);

        return new ElfSymbolModel
        {
            Name = GetString(stringTable, nameOffset),
            Value = value,
            Size = size,
            Bind = bind,
            Type = type,
            Visibility = visibility,
            SectionIndex = sectionIndex,
            SectionName = ResolveSectionNameByIndex(Sections, sectionIndex),
        };
    }

    private static string ResolveSectionNameByIndex(ElfSection[] sections, ushort index)
    {
        if (index == 0) return "UND";
        if (index == (ushort)ElfSectionIndex.Abs) return "ABS";
        if (index == (ushort)ElfSectionIndex.Common) return "COM";

        if (index < sections.Length)
            return sections[index].Name;

        return $"#{index}";
    }

    private byte[] ReadStringTableByIndex(ElfEndianReader reader, uint index)
    {
        if (index == 0 || index >= Sections.Length)
            return [];

        var strSection = Sections[index];
        if (strSection.Size == 0 || strSection.Type != ElfSectionType.Strtab)
            return [];

        return ReadSectionBytes(reader, strSection);
    }
    
    private ElfDynamicEntry[] ReadDynamicTable(ElfEndianReader reader)
    {
        foreach (var section in Sections)
        {
            if (section.Type != ElfSectionType.Dynamic)
                continue;

            var entrySize = Is64Bit ? 16UL : 8UL;
            var count = (int)(section.Size / entrySize);

            var entries = new ElfDynamicEntry[count];
            Seek(reader, section.Offset);

            for (var i = 0; i < count; i++)
            {
                ElfDynamicTag tag;
                ulong val;

                if (Is64Bit)
                {
                    tag = (ElfDynamicTag)reader.ReadInt64();
                    val = reader.ReadUInt64();
                }
                else
                {
                    tag = (ElfDynamicTag)reader.ReadUInt32();
                    val = reader.ReadUInt32();
                }

                if (tag == ElfDynamicTag.Null)
                {
                    Array.Resize(ref entries, i); // !
                    return entries;
                }

                entries[i] = new ElfDynamicEntry { Tag = tag, Val = val };
            }

            // DT_NULL influences at the decoder
            return entries;
        }

        return [];
    }
}

