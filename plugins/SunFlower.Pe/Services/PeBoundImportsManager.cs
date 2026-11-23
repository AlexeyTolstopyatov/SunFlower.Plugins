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
/// Parses the Bound Import Directory (data directory index 11).
/// The table is an array of IMAGE_BOUND_IMPORT_DESCRIPTOR entries; each is
/// optionally followed by NumberOfModuleForwarderRefs IMAGE_BOUND_FORWARDER_REF
/// records. Every module name is stored once, at an offset relative to the
/// start of the directory. Never throws: a damaged directory yields an empty table.
/// </summary>
public class PeBoundImportsManager(ImageDetails info, string path) : DirectoryManager(info), IManager
{
    /// <summary> Data directory index of the bound import descriptor table. </summary>
    private const int BoundImportDirectory = 11;

    /// <summary> A time/date stamp of 0xFFFFFFFF means the module is not bound. </summary>
    private const uint NotBoundStamp = 0xFFFFFFFF;

    /// <summary> Guard against a damaged image advertising an endless table. </summary>
    private const int MaxDescriptors = 4096;

    private readonly ImageDetails _info = info;

    public BoundImportTable BoundImportTable { get; private set; } = new();

    /// <summary> Entry point of this manager. </summary>
    public void Dump()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        BoundImportTable = FillBoundImportTable(reader);
    }

    private BoundImportTable FillBoundImportTable(BinaryReader reader)
    {
        var table = new BoundImportTable();

        if (_info.Directories.Length <= BoundImportDirectory)
            return table;

        var directory = _info.Directories[BoundImportDirectory];
        if (directory.VirtualAddress is 0)
            return table;

        // A single corrupted directory must not abort the whole dump.
        try
        {
            var start = Offset(directory.VirtualAddress);
            var descriptorSize = Marshal.SizeOf<PeBoundImportDescriptor>();
            var forwarderSize = Marshal.SizeOf<PeBoundForwarderReference>();

            // Name offsets are relative to the directory start; when the size is
            // advertised it also bounds the walk.
            var size = directory.Size;
            var end = size > 0 ? start + size : long.MaxValue;
            var position = start;

            for (var i = 0; i < MaxDescriptors; i++)
            {
                if (position + descriptorSize > end)
                    break;

                reader.BaseStream.Position = position;
                var descriptor = Fill<PeBoundImportDescriptor>(reader);

                // An all-zero descriptor terminates the table.
                if (descriptor.TimeDateStamp == 0 &&
                    descriptor.OffsetModuleName == 0 &&
                    descriptor.NumberOfModuleForwarderRefs == 0)
                    break;

                var module = new BoundImportModule
                {
                    Name = ReadName(reader, start, descriptor.OffsetModuleName),
                    TimeDateStamp = descriptor.TimeDateStamp,
                    Bound = descriptor.TimeDateStamp != NotBoundStamp
                };

                position += descriptorSize;

                for (var f = 0; f < descriptor.NumberOfModuleForwarderRefs; f++)
                {
                    if (position + forwarderSize > end)
                        break;

                    reader.BaseStream.Position = position;
                    var forwarder = Fill<PeBoundForwarderReference>(reader);

                    module.Forwarders.Add(new BoundForwarderRef
                    {
                        Name = ReadName(reader, start, forwarder.OffsetModuleName),
                        TimeDateStamp = forwarder.TimeDateStamp
                    });

                    position += forwarderSize;
                }

                table.Modules.Add(module);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Bound imports error: " + ex.Message);
        }

        return table;
    }

    /// <summary> Reads a module name stored at <paramref name="offset"/> from the directory start. </summary>
    private static string ReadName(BinaryReader reader, long directoryStart, ushort offset)
    {
        if (offset == 0)
            return string.Empty;

        try
        {
            reader.BaseStream.Position = directoryStart + offset;
            return ReadCString(reader);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Bound import name error: " + ex.Message);
            return string.Empty;
        }
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