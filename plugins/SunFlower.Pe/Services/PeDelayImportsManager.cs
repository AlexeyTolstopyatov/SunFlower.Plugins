using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

///
/// CoffeeLake 2024-2026
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
///
/// <summary>
/// Parses the Delay Import Directory (data directory index 13).
/// The delay import descriptor table is terminated by an all-zero descriptor;
/// every descriptor points at a name table (INT) whose entries are either an
/// ordinal or an RVA to an IMAGE_IMPORT_BY_NAME structure.
/// Never throws: a damaged directory yields an empty table.
/// </summary>
public class PeDelayImportsManager(ImageDetails info, string path) : DirectoryManager(info), IManager
{
    /// <summary> Data directory index of the delay import descriptor table. </summary>
    private const int DelayImportDirectory = 13;

    /// <summary> grAttrs flag "dlattrRva": descriptor fields are RVAs, not VAs. </summary>
    private const uint DescriptorRvaFlag = 0x00000001;

    /// <summary> Guard against a damaged image advertising an endless table. </summary>
    private const int MaxDescriptors = 4096;

    private readonly ImageDetails _info = info;

    public DelayImportTable DelayImportTable { get; private set; } = new();

    /// <summary> Entry point of this manager. </summary>
    public void Dump()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        DelayImportTable = FillDelayImportTable(reader);
    }

    private DelayImportTable FillDelayImportTable(BinaryReader reader)
    {
        var table = new DelayImportTable();

        if (_info.Directories.Length <= DelayImportDirectory)
            return table;

        // The descriptor table is self-terminating, so a zero Size (seen in
        // some images) must not hide a valid VirtualAddress.
        var directory = _info.Directories[DelayImportDirectory];
        if (directory.VirtualAddress == 0)
            return table;

        // A single corrupted directory must not abort the whole dump.
        try
        {
            var descriptorOffset = Offset(directory.VirtualAddress);

            // Collect the descriptors first: reading names/thunks moves the
            // reader away, so the table must be walked in its own pass.
            List<PeDelayImportDescriptor> descriptors = [];
            var descriptorSize = Marshal.SizeOf<PeDelayImportDescriptor>();
            for (var i = 0; i < MaxDescriptors; i++)
            {
                reader.BaseStream.Position = descriptorOffset + (long)i * descriptorSize;
                var descriptor = Fill<PeDelayImportDescriptor>(reader);

                // The table is terminated by an all-zero descriptor.
                if (descriptor.Attributes == 0 &&
                    descriptor.Name == 0 &&
                    descriptor.Iat == 0 &&
                    descriptor.Int == 0)
                    break;

                descriptors.Add(descriptor);
            }

            foreach (var descriptor in descriptors)
            {
                var usesRva = (descriptor.Attributes & DescriptorRvaFlag) != 0;

                var module = new DelayImportModule
                {
                    Attributes = descriptor.Attributes,
                    TimeStamp = descriptor.TimeStamp,
                    UsesRva = usesRva,
                    // A non-zero time stamp means the ImportAddressTable was bound ahead of time.
                    Bound = descriptor.TimeStamp != 0,
                    Name = ReadAsciiZ(reader, Rva(descriptor.Name, usesRva))
                };

                // The INT (name table) is authoritative; the ImportAddressTable is only a
                // fallback for images that left INT empty.
                var thunkTable = descriptor.Int != 0 ? descriptor.Int : descriptor.Iat;
                var functions = ReadThunks(reader, Rva(thunkTable, usesRva), usesRva);
                foreach (var function in functions)
                    function.Module = module.Name;

                module.Functions.AddRange(functions);
                table.Modules.Add(module);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Delay imports error: " + ex.Message);
        }

        return table;
    }

    /// <summary>
    /// Delay descriptors normally store RVAs, but when <c>dlattrRva</c> is
    /// clear the fields are Virtual Addresses; convert them back to RVAs.
    /// </summary>
    private uint Rva(uint value, bool usesRva)
        => usesRva ? value : value - _info.ImageBase;

    /// <summary>
    /// Reads the INT: a zero-terminated array of thunk entries. Each entry is
    /// pointer-sized (4 bytes in PE32, 8 bytes in PE32+): the high bit marks an
    /// ordinal, otherwise the low 32 bits are an RVA to IMAGE_IMPORT_BY_NAME.
    /// </summary>
    private List<ImportFunction> ReadThunks(BinaryReader reader, uint intRva, bool usesRva)
    {
        List<ImportFunction> result = [];
        if (intRva == 0)
            return result;

        var entrySize = _info.Is64Bit ? 8 : 4;
        var ordinalFlag = _info.Is64Bit ? 0x8000000000000000UL : 0x80000000UL;
        var ordinalMask = _info.Is64Bit ? 0x7FFFFFFFFFFFFFFFUL : 0x7FFFFFFFUL;

        try
        {
            var thunkOffset = Offset(intRva);
            while (true)
            {
                reader.BaseStream.Position = thunkOffset;

                var thunk = _info.Is64Bit
                    ? reader.ReadUInt64()
                    : reader.ReadUInt32();
                if (thunk == 0)
                    break;

                if ((thunk & ordinalFlag) != 0)
                {
                    var ordinal = thunk & ordinalMask;
                    result.Add(new ImportFunction
                    {
                        Name = $"@{ordinal}",
                        Ordinal = (uint)ordinal,
                        Hint = 0,
                        Address = thunk
                    });
                }
                else
                {
                    // The RVA to IMAGE_IMPORT_BY_NAME is kept in the low 32 bits.
                    reader.BaseStream.Position = Offset(Rva((uint)(thunk & 0xFFFFFFFF), usesRva));

                    var hint = reader.ReadUInt16();
                    result.Add(new ImportFunction
                    {
                        Name = ReadCString(reader),
                        Hint = hint
                    });
                }

                thunkOffset += entrySize;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Delay thunk error: " + ex.Message);
        }

        return result;
    }

    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="rva">RVA of an ASCIIZ string</param>
    private string ReadAsciiZ(BinaryReader reader, uint rva)
    {
        reader.BaseStream.Seek(Offset(rva), SeekOrigin.Begin);
        return ReadCString(reader);
    }

    /// <param name="reader"> <see cref="BinaryReader"/> instance </param>
    /// <returns> ASCII terminated string <c>TSTR</c> </returns>
    private static string ReadCString(BinaryReader reader)
    {
        List<byte> bytes = [];
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);

        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}