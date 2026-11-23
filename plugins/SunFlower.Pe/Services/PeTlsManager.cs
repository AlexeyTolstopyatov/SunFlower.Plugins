using System.Diagnostics;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

//
// CoffeeLake 2024-*
// This code is JellyBins part for dumping
// Windows PE32/+ images.
//
// Licensed under MIT
//
/// <summary>
/// Parses the Thread Local Storage directory
/// Unlike most directories the TLS fields are Virtual Addresses, not RVAs, so
/// they are converted with the full 64-bit image base.
///
/// The callback array is
/// read because its entries run before the entry point and therefore are
/// additional entry points for a disassembler.
/// Never throws a damaged directory yields an empty model.
/// </summary>
public class PeTlsManager(ImageDetails info, string path) : DirectoryManager(info), IManager
{
    /// <summary> Guard against a damaged callback array. </summary>
    private const int MaxCallbacks = 1024;

    private readonly ImageDetails _info = info;

    public TlsDirectoryModel TlsDirectory { get; private set; } = new();

    /// <summary> Entry point of this manager. </summary>
    public void Dump()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        TlsDirectory = FillTlsDirectory(reader);
    }

    private TlsDirectoryModel FillTlsDirectory(BinaryReader reader)
    {
        var model = new TlsDirectoryModel();
        
        var directory = _info.Directories[9];
        
        if (!IsDirectoryExists(_info.Directories[9]))
            return model;

        // A single corrupted directory must not abort the whole dump.
        try
        {
            reader.BaseStream.Seek(Offset(directory.VirtualAddress), SeekOrigin.Begin);

            ulong start, end, index, callbacks;
            uint zeroFill, characteristics;

            if (_info.Is64Bit)
            {
                var tls = Fill<PeTlsDirectory64>(reader);
                start = tls.StartAddressOfRawData;
                end = tls.EndAddressOfRawData;
                index = tls.AddressOfIndex;
                callbacks = tls.AddressOfCallBacks;
                zeroFill = tls.SizeOfZeroFill;
                characteristics = tls.Characteristics;
            }
            else
            {
                var tls = Fill<PeTlsDirectory32>(reader);
                start = tls.StartAddressOfRawData;
                end = tls.EndAddressOfRawData;
                index = tls.AddressOfIndex;
                callbacks = tls.AddressOfCallBacks;
                zeroFill = tls.SizeOfZeroFill;
                characteristics = tls.Characteristics;
            }

            model.Present = true;
            model.StartAddressOfRawData = start;
            model.EndAddressOfRawData = end;
            model.AddressOfIndex = index;
            model.AddressOfCallBacks = callbacks;
            model.SizeOfZeroFill = zeroFill;
            model.Characteristics = characteristics;

            model.StartRva = ToRva(start);
            model.EndRva = ToRva(end);
            model.IndexRva = ToRva(index);
            model.CallBacksRva = ToRva(callbacks);

            model.Callbacks.AddRange(ReadCallbacks(reader, callbacks));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("TLS error: " + ex.Message);
        }

        return model;
    }

    /// <summary>
    /// TLS fields are Virtual Addresses; subtract the image base to get an RVA.
    /// Returns 0 for a null address or one below the image base.
    /// </summary>
    private uint ToRva(ulong va)
        => va < _info.ImageBase64 ? 0u : (uint)(va - _info.ImageBase64);

    /// <summary> Reads the terminated array of callback Virtual Addresses. </summary>
    private List<ulong> ReadCallbacks(BinaryReader reader, ulong callbacksVa)
    {
        List<ulong> result = [];
        if (callbacksVa == 0)
            return result;

        var rva = ToRva(callbacksVa);
        if (rva == 0)
            return result;

        var entrySize = _info.Is64Bit ? 8 : 4;

        try
        {
            var offset = Offset(rva);
            for (var i = 0; i < MaxCallbacks; i++)
            {
                reader.BaseStream.Position = offset + (long)i * entrySize;

                var value = _info.Is64Bit
                    ? reader.ReadUInt64()
                    : reader.ReadUInt32();
                if (value == 0)
                    break;

                result.Add(value);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{nameof(FillTlsDirectory)}: " + ex.Message);
        }

        return result;
    }
}