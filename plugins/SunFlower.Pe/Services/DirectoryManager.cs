using SunFlower.Pe.Exceptions;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;
using Directory = SunFlower.Pe.Models.Directory;

namespace SunFlower.Pe.Services;
///
/// CoffeeLake 2024-2025
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
/// 

/// <summary>
/// Directory manager must init following toolchain
/// </summary>
public class DirectoryManager(ImageDetails info) : UnsafeManager
{
    /// <returns> Directory exists when someone of 2 parameters not 0 </returns>
    protected static bool IsDirectoryExists(Directory dir)
    {
        return dir is { Size: not null, VirtualAddress: not null };
    }
    /// <param name="rva"> Required RVA </param>
    /// <returns> File offset from RVA of selected section </returns>
    /// <exception cref="SectionNotFoundException"> If RVA not belongs to any section </exception>
    protected long Offset(long rva)
    {
        var section = Section(rva);
        
        return 0 + section.PointerToRawData + (rva - section.VirtualAddress);
    }
    /// <param name="rva"> Required relative address </param>
    /// <returns> <see cref="PeSection"/> Which RVA belongs </returns>
    /// <exception cref="SectionNotFoundException"> If RVA not belongs to any section </exception>
    private PeSection Section(long rva)
    {
        // RVA is always a 32-bit value.
        var rva32 = Convert.ToUInt32(rva);

        foreach (var section in info.Sections.OrderBy(s => s.VirtualAddress))
        {
            // Packed / unusual images often store VirtualSize == 0 while the
            // section still maps raw bytes, so fall back on SizeOfRawData.
            var spanSize = section.VirtualSize > section.SizeOfRawData
                ? section.VirtualSize
                : section.SizeOfRawData;

            if (spanSize == 0)
                continue;

            // Compare in 64-bit to avoid uint overflow on VirtualAddress + size.
            var start = Convert.ToUInt64(section.VirtualAddress);
            var end = start + Convert.ToUInt64(spanSize);
            if (Convert.ToUInt64(rva32) >= start && Convert.ToUInt64(rva32) < end)
            {
                return section;
            }
        }
        throw new SectionNotFoundException();
    }
    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="rva">RVA</param>
    /// <param name="count">count of elements in segment</param>
    /// <typeparam name="T">type of array-segment</typeparam>
    /// <returns>Array of structures</returns>
    protected T[] ReadArray<T>(BinaryReader reader, uint rva, uint count) where T : struct
    {
        var offset = Offset(rva);
        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
        var result = new T[count];
        for (var i = 0; i < count; i++)
            result[i] = Fill<T>(reader);
        return result;
    }
}