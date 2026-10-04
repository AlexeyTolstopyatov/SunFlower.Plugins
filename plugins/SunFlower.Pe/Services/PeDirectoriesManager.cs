using SunFlower.Pe.Headers;
using Directory = SunFlower.Pe.Models.Directory;

namespace SunFlower.Pe.Services;

///
/// CoffeeLake 2024-2026
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
///
/// <summary>
/// Reads the Data Directory table of a PE32/+ image.
/// Each entry is a { RVA, Size } pair describing where an optional
/// structure (exports, imports, resources, ...) lives in the image.
/// </summary>
public class PeDirectoriesManager
{
    public Directory[] Directories { get; }

    private static readonly string[] DirectoryNames =
    [
        "EXPORTS",
        "IMPORTS",
        "RESOURCES",
        "EXCEPTIONS",
        "SECURITY",
        "BASE RELOCATIONS",
        "DEBUG",
        "ARCHITECTURE",
        "GLOBAL POINTER",
        "TLS",
        "LOAD CONFIG",
        "BOUND IMPORTS",
        "IMPORT ADDRESSES",
        "DELAYED IMPORTS",
        "COM DESCRIPTOR",
        "#15"
    ];
    /// <param name="reader"><see cref="BinaryReader"/> instance</param>
    /// <param name="offset">Absolute file offset of the directory table</param>
    /// <param name="count">Number of entries to read (already clamped to 16)</param>
    public PeDirectoriesManager(BinaryReader reader, long offset, uint count)
    {
        reader.BaseStream.Position = offset;
        var result = new Directory[count];

        for (var i = 0; i < count; i++)
        {
            var va = reader.ReadUInt32();
            var sz = reader.ReadUInt32();
            
            result[i] = new Directory(va, sz, DirectoryNames[i]);
        }

        Directories = result;
    }
}