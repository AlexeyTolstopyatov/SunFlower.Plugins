using SunFlower.Pe.Headers;
using SunFlower.Pe.Services;

namespace SunFlower.Pe.Models;

/// <summary>
/// Important details about PE32/+
/// for <see cref="PeExportsManager"/>
/// </summary>
public class ImageDetails
{
    public bool Is64Bit { get; set; }
    public uint NumberOfSections { get; set; }
    public uint NumberOfRva { get; set; }
    
    public uint SectionAlignment { get; set; }
    public uint FileAlignment { get; set; }
    public uint SizeOfImage { get; set; }
    public uint ImageBase { get; set; }

    /// <summary>
    /// Full 64-bit image base. <see cref="ImageBase"/> keeps only the low 32
    /// bits for the 32-bit consumers; structures that store Virtual Addresses
    /// (TLS) need the exact base to convert a VA back to an RVA.
    /// </summary>
    public ulong ImageBase64 { get; set; }

    public uint BaseOfCode { get; set; }
    public uint BaseOfData { get; set; }
    
    public PeSection[] Sections { get; set; } = [];
    public Directory[] Directories { get; set; } = [];
    public uint EntryPoint { get; set; }
}