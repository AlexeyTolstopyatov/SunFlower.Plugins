using System.Diagnostics;
using System.Text;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

///
/// CoffeeLake 2024-2025
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
/// 

/// <summary>
/// Opens stream and makes dump for physical sections
/// in PE32/+ required image
/// </summary>
/// <param name="info"></param>
public class PeExportsManager(ImageDetails info, string path) : DirectoryManager(info), IManager
{
    private readonly ImageDetails _info = info;
    public ExportTable ExportTable { get; private set; } = new();

    // Declare Imports Exports CRT BaseRelocs (and other) sections here.
    public void Dump()
    {
        // run main process
        FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        BinaryReader reader = new(stream);

        ExportTable = FillExportTableModel(reader);
        
        reader.Close();
    }

    private ExportTable FillExportTableModel(BinaryReader reader)
    {
        // make sure: ExportsDirectory exists
        if (_info.Directories.Length == 0 || !IsDirectoryExists(_info.Directories[0]))
            return new ExportTable();

        var model = new ExportTable();

        // A single corrupted directory must not abort the whole dump:
        // fall back to an empty table instead.
        try
        {
            var exportOffset = Offset(_info.Directories[0].VirtualAddress ?? 0);

            reader.BaseStream.Seek(exportOffset, SeekOrigin.Begin);
            var exportDir = Fill<PeImageExportDirectory>(reader);
            model.ExportDirectory = exportDir;

            reader.BaseStream.Position = Offset(exportDir.Name);

            var moduleName = ReadImportString(reader);
            Debug.WriteLine(moduleName);

            // Guard against absurd counts advertised by damaged images.
            var functionCount = exportDir.NumberOfFunctions;
            if (functionCount > 1000000)
                return new ExportTable();

            // Names can never exceed the number of function slots.
            var nameCount = exportDir.NumberOfNames;
            if (nameCount > functionCount)
                nameCount = functionCount;

            // RVA is always a 32-bit value regardless of PE32/PE32+.
            var functionAddresses = ReadArray<uint>(reader, exportDir.AddressOfFunctions, functionCount);
            var namePointers = ReadArray<uint>(reader, exportDir.AddressOfNames, nameCount);
            var ordinals = ReadArray<ushort>(reader, exportDir.AddressOfNameOrdinals, nameCount);

            // Built a name per ordinal slot; entries exported by ordinal only
            // (present in the function table but missing a name) still appear.
            var namesByIndex = new string[functionCount];
            for (uint i = 0; i < functionCount; i++)
                namesByIndex[i] = string.Empty;

            for (uint i = 0; i < nameCount; i++)
            {
                var index = ordinals[i];
                if (index < functionCount)
                    namesByIndex[index] = ReadExportString(reader, namePointers[i]);
            }

            for (uint i = 0; i < functionCount; i++)
            {
                var functionName = namesByIndex[i].Length != 0
                    ? namesByIndex[i]
                    : "func_" + (exportDir.Base + i);

                model.Functions.Add(new ExportFunction
                {
                    Name = functionName,
                    Ordinal = exportDir.Base + i,
                    Address = functionAddresses[i]
                });
            }

            return model;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Exports error: " + ex.Message);
            return new ExportTable();
        }
    }
    
    /// <param name="reader"> <see cref="BinaryReader"/> instance </param>
    /// <returns> ASCIIZ typed string <c>TSTR</c> </returns>
    private static string ReadImportString(BinaryReader reader)
    {
        List<byte> bytes = [];
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);
        
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="rva">rva of entry name</param>
    /// <returns> ASCII(Z) string of exported entry</returns>
    private string ReadExportString(BinaryReader reader, uint rva)
    {
        var offset = Offset(rva);
        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
        List<byte> bytes = [];
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}