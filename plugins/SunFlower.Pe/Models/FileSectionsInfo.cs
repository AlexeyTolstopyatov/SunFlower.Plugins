using SunFlower.Pe.Headers;
using SunFlower.Pe.Services;

namespace SunFlower.Pe.Models;

/// <summary>
/// Important details about PE32/+
/// for <see cref="PeExportsManager"/>
/// </summary>
public class FileSectionsInfo
{
    public bool Is64Bit { get; set; }
    public uint NumberOfSections { get; set; }
    public uint NumberOfRva { get; set; }
    
    public uint SectionAlignment { get; set; }
    public uint FileAlignment { get; set; }
    
    public uint ImageBase { get; set; }
    public uint BaseOfCode { get; set; }
    public uint BaseOfData { get; set; }
    
    public PeSection[] Sections { get; set; } = [];
    public PeDirectory[] Directories { get; set; } = [];
    public uint EntryPoint { get; set; }
}