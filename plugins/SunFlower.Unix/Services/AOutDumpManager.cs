using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using SunFlower.Abstractions;
using SunFlower.Unix.Format;
using SunFlower.Unix.Models;

namespace SunFlower.Unix.Services;

///
/// CoffeeLake 2026-*
/// This code is part of the SunFlower.Unix plugin for dumping
/// `a.out` executable/shared images (Unix, Minix, *BSD).
///
/// Licensed under MIT
///
public class AOutDumpManager(string path) : UnsafeManager
{
    /// <summary>
    /// Parsed `exec` header of `a.out` binary.
    /// </summary>
    public Header Header { get; private set; }

    /// <summary>
    /// Raw magic word (host byte-order) read from the file.
    /// </summary>
    public ushort RawMagic { get; private set; }

    /// <summary>
    /// Offset of the text segment in the file (computed by conventions
    /// depending on the magic number).
    /// </summary>
    public long TextOffset { get; private set; }

    /// <summary>
    /// Offset of the symbol table in the file.
    /// </summary>
    public long SymbolOffset { get; private set; }

    /// <summary>
    /// Offset of the string table in the file.
    /// </summary>
    public long StringOffset { get; private set; }

    /// <summary>
    /// Decoded symbol table entries (name + raw `nlist` record).
    /// </summary>
    public IReadOnlyList<AOutSymbol> Symbols { get; private set; } = [];

    public void Initialize()
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

        // Validate and read the magic word.
        var firstWord = reader.ReadUInt16();
        RawMagic = firstWord;

        // The header layout in this project treats the first 8 bytes
        // (magic + flags + cpu + header length + unused + version) as one block,
        // and the rest as ulong-sized fields. So we can rewind and fill the
        // entire struct sequentially.
        reader.BaseStream.Position = 0;
        var header = Fill<Header>(reader);

        Header = header;
        DebugPrint(FlowerReflection.DictionaryDataTable(header));
        // Verify that the magic number is known.
        MagicWord magic = header.MagicWord;
        if (!IsKnownMagic(magic))
            throw new InvalidDataException(
                $"File is not a supported a.out image. (captured 0x{magic:X})");

        // Compute section offsets using the documented macros.
        TextOffset = ComputeTextOffset(magic);
        
        SymbolOffset = TextOffset + (long)header.TextSize + (long)header.DataSize
                       + (long)header.TextRelocSize + (long)header.DataRelocSize;
        StringOffset = SymbolOffset + (long)header.Symbols;

        // Read and resolve the symbol table (if present).
        if (header.Symbols > 0)
            Symbols = ReadSymbols(reader);
    }
    private void DebugPrint(DataTable table)
    {
        foreach (DataRow r in table.Rows)
        {
            foreach (DataColumn c in table.Columns)
            {
                Console.WriteLine($"{r[c]}");
            }
        }
    }
    /// <summary>
    /// Reads the string table (a 4-byte length followed by NUL-terminated
    /// ASCII strings) and then decodes every `struct nlist` record,
    /// resolving its name from the string table.
    /// </summary>
    private IReadOnlyList<AOutSymbol> ReadSymbols(BinaryReader reader)
    {
        var strBytes = ReadStringTableBytes(reader);
        var symbolCount = Header.Symbols / (uint)Marshal.SizeOf<Nlist>();

        List<AOutSymbol> symbols = new((int)symbolCount);
        reader.BaseStream.Position = SymbolOffset;

        for (uint i = 0; i < symbolCount; ++i)
        {
            var raw = Fill<Nlist>(reader);
            var name = ResolveName(strBytes, raw.Strx);
            symbols.Add(new AOutSymbol(raw, name));
        }

        return symbols;
    }

    /// <summary>
    /// Loads the whole string table into memory. The first 4 bytes hold the
    /// total table size (its minimum value is 4), the rest is concatenated
    /// NUL-terminated strings.
    /// </summary>
    private byte[] ReadStringTableBytes(BinaryReader reader)
    {
        // Exceptions 
        reader.BaseStream.Position = StringOffset;

        Console.WriteLine($"Position set: 0x{StringOffset:X}");

        // Stream bounds
        var tableLength = reader.ReadUInt32();
        Console.WriteLine($"Table len: {tableLength}");

        if (tableLength >= 4 && tableLength <= Math.Abs(reader.BaseStream.Length - StringOffset))
        {
            Console.WriteLine("Captured!");
            return reader.ReadBytes((int)tableLength);
        }

        Console.WriteLine($"Fallback len: {reader.BaseStream.Length - reader.BaseStream.Position}");
        // Fallback: the declared length is inconsistent — read to EOF.
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        return reader.ReadBytes((int)remaining);
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII string from the string table starting at
    /// the given byte offset. A zero offset (or out-of-range) yields an empty name.
    /// </summary>
    private static string ResolveName(byte[] table, uint strx)
    {
        if (strx == 0 || strx >= table.Length)
            return string.Empty;

        int start = (int)strx;
        int end = start;
        while (end < table.Length && table[end] != 0)
            ++end;

        var safeName = FlowerReport.SafeString(Encoding.ASCII.GetString(table, start, end - start));

        Console.WriteLine(safeName);

        return safeName;
    }

    private static bool IsKnownMagic(MagicWord magic)
    {
        if (magic == MagicWord.OMAGIC)
            return true;
        if (magic == MagicWord.NMAGIC)
            return true;
        if (magic == MagicWord.XMAGIC)
            return true;
        if (magic == MagicWord.ZMAGIC)
            return true;
        if (magic == MagicWord.QMAGIC)
            return true;
        if (magic == MagicWord.MINIX_MAGIC)
            return true;

        return false;
    }

    private static long ComputeTextOffset(MagicWord magic)
    {
        // For ZMAGIC/XMAGIC/QMAGIC the text segment begins at byte 1024.
        // For OMAGIC/NMAGIC the text immediately follows the exec header.
        return magic switch
        {
            MagicWord.QMAGIC => 4096,
            MagicWord.ZMAGIC => 1024,
            MagicWord.XMAGIC => 1024,
            _ => Marshal.SizeOf<Header>() // or a_hdrsize
        };
    }
}