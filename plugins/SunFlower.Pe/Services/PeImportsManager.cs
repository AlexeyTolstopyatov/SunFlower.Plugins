using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;
///
/// CoffeeLake 2024-2025-*
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
/// 
public class PeImportsManager(ImageDetails info, string path) : DirectoryManager(info), IManager
{
    private readonly ImageDetails _info = info;
    public ImportNameTable ImportNameTable { get; private set; } = new();
    public ImportAddressTable? ImportAddressTable { get; private set; }

    /// <summary> Deserializes bytes segment to import entries table </summary>
    /// <param name="reader">your content reader instance</param>
    /// <returns> Done <see cref="ImportNameTable"/> structure </returns>
    private ImportNameTable FillImportTableModel(BinaryReader reader)
    {
        ImportNameTable dump = new();
        
        if (!IsDirectoryExists(_info.Directories[1]))
            return new ImportNameTable();
        
        try
        {
            reader.BaseStream.Seek(Offset(_info.Directories[1].VirtualAddress), SeekOrigin.Begin);
            List<PeImportDescriptor> items = [];
            while (true)
            {
                var item = Fill<PeImportDescriptor>(reader);
                if (item.OriginalFirstThunk == 0 && item.FirstThunk == 0) 
                    // Being honest, I don't ever know situations when FirstThunk
                    // was zeroed. That's why use it to comparison together with original thunk
                    break;

                items.Add(item);
            }

            foreach (var item in items)
            {
                reader.BaseStream.Seek(Offset(item.Name), SeekOrigin.Begin);
                byte[] name = [];
                while (true)
                {
                    var b = Fill<byte>(reader);
                    if (b == 0) break;

                    var dllName = new byte[name.Length + 1];
                    name.CopyTo(dllName, 0);
                    dllName[name.Length] = b;
                    name = dllName;
                }
            }

            List<ImportModule> modules = [];
            foreach (var descriptor in items)
            {
                modules.Add(ReadImportDll(reader, descriptor));
            }

            dump.Modules = modules;
        }
        catch(Exception exn)
        {
            Console.WriteLine($"{nameof(PeImportsManager)}::{nameof(FillImportTableModel)}: {exn}");
        }
        
        return dump;
    }

    private ImportAddressTable FillImportAddressTable(BinaryReader reader)
    {
        var table = new ImportAddressTable();

        var directory = _info.Directories[1];
        if (directory.VirtualAddress == 0) 
            return table;

        uint iatDirRva = 0, iatDirSize = 0;
        if (_info.Directories.Length > 12)
        {
            iatDirRva  = _info.Directories[12].VirtualAddress;
            iatDirSize = _info.Directories[12].Size;
        }
        _ = iatDirRva; _ = iatDirSize; // sanitarium check

        var descriptorSize = Marshal.SizeOf<PeImportDescriptor>();
        var ptrSize = _info.Is64Bit ? 8 : 4;

        try
        {
            var descriptorOffset = Offset(directory.VirtualAddress);

            for (var i = 0; i < 0x1000; i++)
            {
                reader.BaseStream.Position = descriptorOffset + (long)i * descriptorSize;
                var desc = Fill<PeImportDescriptor>(reader);

                if (desc.OriginalFirstThunk == 0 &&
                    desc.FirstThunk == 0 &&
                    desc.Name == 0)
                    break;
                
                // Import Name Table never be missed by Microsoft docs, so,
                // Original chunk might be zeroed. That's pretty popular synopsis
                var intRva = desc.OriginalFirstThunk != 0 
                    ? desc.OriginalFirstThunk 
                    : desc.FirstThunk;
                
                var iatRva = desc.FirstThunk;

                var module = new AddressTableModule
                {
                    Name          = ReadCStringAt(reader, desc.Name),
                    FirstThunkRva = iatRva
                };

                var names = ReadThunkNames(reader, intRva);
                var slots = ReadThunkSlots(reader, iatRva, names.Count, ptrSize);

                for (var j = 0; j < names.Count; j++)
                {
                    var slotRva = (uint)(iatRva + j * ptrSize);
                    
                    module.Functions.Add(new AddressTableFunction
                    {
                        Name      = names[j].Name,
                        Hint      = names[j].Hint,
                        Ordinal   = names[j].Ordinal,
                        IsOrdinal = names[j].IsOrdinal,
                        SlotRva   = slotRva,
                        ThunkValue = slots.Count > j ? slots[j] : 0
                    });
                }

                table.Modules.Add(module);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Imports error: " + ex.Message);
        }

        return table;
    }
    
    /// <param name="reader"> Current instance of <see cref="BinaryReader"/> </param>
    /// <param name="descriptor"> Seeking <see cref="PeImportDescriptor"/> table </param>
    /// <returns> Filled <see cref="ImportModule"/> instance full of module information </returns>
    private ImportModule ReadImportDll(BinaryReader reader, PeImportDescriptor descriptor)
    {
        var nameOffset = Offset(descriptor.Name);
        reader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
        var dllName = ReadCString(reader);
        
        var oft = 
            ReadIntThunk(reader, descriptor.OriginalFirstThunk, "[OFT]")
                .Select(x => { x.Module = dllName; return x; });

        // var ft = 
        //     ReadIntThunk(reader, descriptor.FirstThunk, "[FT]");

        List<ImportFunction> functions = [];
        functions.AddRange(oft);
        // functions.AddRange(ft); // <-- may contains duplicates of imported entries

        return new ImportModule
        {
            Functions = functions
        };
    }
    /// <summary> Use it when application requires 32bit machine WORD </summary>
    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="thunkRva">RVA of procedures block</param>
    /// <param name="tag">debug information (#debug only)</param>
    /// <returns>List of imported functions</returns>
    private List<ImportFunction> ReadIntThunk(BinaryReader reader, uint thunkRva, string tag)
    {
        var sizeOfThunk = _info.Is64Bit ? 8 : 4;
        var ordinalBit = _info.Is64Bit ? 0x8000000000000000 : 0x80000000;
        var ordinalMask = _info.Is64Bit ? 0x7FFFFFFFFFFFFFFFu : 0x7FFFFFFFu;
    
        List<ImportFunction> result = [];
        if (thunkRva == 0) 
            return result;
    
        try
        {
            var thunkOffset = Offset(thunkRva);
            while (true)
            {
                reader.BaseStream.Position = thunkOffset;
            
                var thunkValue = _info.Is64Bit 
                    ? reader.ReadUInt64() 
                    : reader.ReadUInt32();
            
                if (thunkValue == 0) 
                    break;
                
                // IMPORT by @ordinal
                if ((thunkValue & ordinalBit) != 0)
                {
                    result.Add(new ImportFunction
                    {
                        Name = $"@{thunkValue & ordinalMask}",
                        Ordinal = (uint)(thunkValue & ordinalMask),
                        Hint = 0,
                        Address = thunkValue
                    });
                }
                // IMPORT by name
                else
                {
                    var nameAddr = Offset((uint)thunkValue);
                    reader.BaseStream.Position = nameAddr;
                
                    var hint = reader.ReadUInt16();
                    var name = ReadCString(reader);
                
                    result.Add(new ImportFunction
                    {
                        Name = name,
                        Hint = hint,
                        Address = thunkValue
                    });
                }
            
                thunkOffset += sizeOfThunk;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{tag} error: {ex.Message}");
        }

        return result;
    }
    private List<(string Name, ushort Hint, uint Ordinal, bool IsOrdinal)>
        ReadThunkNames(BinaryReader reader, uint intRva)
    {
        var result = new List<(string, ushort, uint, bool)>();
        if (intRva == 0) return result;

        var ptrSize     = _info.Is64Bit ? 8 : 4;
        var ordinalFlag = _info.Is64Bit ? 0x8000000000000000UL : 0x80000000UL;
        var ordinalMask = _info.Is64Bit ? 0x7FFFFFFFFFFFFFFFUL : 0x7FFFFFFFUL;

        try
        {
            var offset = Offset(intRva);
            while (true)
            {
                reader.BaseStream.Position = offset;
                var thunk = _info.Is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
                if (thunk == 0) break;

                if ((thunk & ordinalFlag) != 0)
                {
                    var ord = (uint)(thunk & ordinalMask);
                    result.Add(($"@{ord}", 0, ord, true));
                }
                else
                {
                    reader.BaseStream.Position = Offset((uint)(thunk & 0xFFFFFFFF));
                    var hint = reader.ReadUInt16();
                    result.Add((ReadCString(reader), hint, 0, false));
                }

                offset += ptrSize;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("INT error: " + ex.Message);
        }

        return result;
    }

    private List<ulong> ReadThunkSlots(BinaryReader reader, uint iatRva, int count, int ptrSize)
    {
        var result = new List<ulong>(count);
        if (iatRva == 0 || count == 0) return result;

        try
        {
            var offset = Offset(iatRva);
            for (var i = 0; i < count; i++)
            {
                reader.BaseStream.Position = offset + (long)i * ptrSize;
                var v = _info.Is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
                if (v == 0) break;
                result.Add(v);
            }
        }
        catch (Exception ex) { Debug.WriteLine("IAT error: " + ex.Message); }

        return result;
    }

    private string ReadCStringAt(BinaryReader reader, uint rva)
    {
        if (rva == 0) return string.Empty;
        reader.BaseStream.Position = Offset(rva);
        return ReadCString(reader);
    }

    /// <param name="reader"> <see cref="BinaryReader"/> instance </param>
    /// <returns> ASCII terminated string <c>TSTR</c> </returns>
    /// <remarks>terminated means zeroed (has <c>\0</c> at the end) </remarks>
    private static string ReadCString(BinaryReader reader)
    {
        List<byte> bytes = [];
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);
        
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
    /// <summary>
    /// Entry point of this manager
    /// </summary>
    public void Dump()
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        BinaryReader reader = new(stream);

        ImportNameTable = FillImportTableModel(reader);
        ImportAddressTable = FillImportAddressTable(reader);
        
        reader.Close();
    }
}