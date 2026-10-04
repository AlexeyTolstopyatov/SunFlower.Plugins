using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Services;

///
/// CoffeeLake 2024-2026
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
///
/// <summary>
/// Reads the Section Table of a PE32/+ image.
/// Section headers follow the optional header and are fixed-size (40 bytes),
/// so a plain sequential read from the computed offset is enough.
/// </summary>
public class PeSectionsManager : UnsafeManager
{
    public PeSection[] Sections { get; }

    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="offset">Absolute file offset of the section table</param>
    /// <param name="count">Number of sections to read (already clamped)</param>
    public PeSectionsManager(BinaryReader reader, long offset, uint count)
    {
        reader.BaseStream.Position = offset;

        var result = new PeSection[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = Fill<PeSection>(reader);
        }

        Sections = result;
    }
}