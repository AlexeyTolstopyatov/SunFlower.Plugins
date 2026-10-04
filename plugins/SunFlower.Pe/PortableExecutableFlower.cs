//
// CoffeeLake (C) 2026-*
// 
// The PortableExecutableFlower.cs represents pure PE32/+ dumping plugin
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;
using SunFlower.Pe.Services;
using Directory = SunFlower.Pe.Models.Directory;

namespace SunFlower.Pe;

[Flower(FlowerTarget.Data)]
[FlowerVersionContract(5, 0, 0)]
public class PortableExecutableFlower : IFlower
{
    [Seed(
        name: "Portable Executable Header",
        description: "COFF header or file header is a mandatory structure in all `PE` modules\r\n" +
                     "The File Header is a structure that holds some information about the PE file."
    )]
    public PeFileHeader FileHeader { get; set; }
    
    [Seed(
        name: "Portable Executable Optional Header",
        description: "The Optional Header is the most important header of the NT headers, \r\n" +
                     "the PE loader looks for specific information provided by that header to be able to load and run the executable.\r\n" +
                     "It’s called the optional header because some file types like object files don’t have it, \r\n" +
                     "however this header is essential for image files."
    )]
    public PeOptionalHeader OptionalHeader { get; set; }

    [Seed(
        name: "Data Directory Table",
        description: "Data Directories are the pieces of data located somewhere in any section of the PE."
    )] 
    public Directory[] Directories { get; set; } = [];
    
    [Seed(
        name: "Section Table",
        description: "Sections are the containers of the actual data of the executable file,\r\n " +
                     "they occupy the rest of the PE file after the headers, " +
                     "precisely after the section headers."
    )]
    public PeSection[] Sections { get; set; } = [];

    [Seed(
        name: "ImageDetails Anomalies",
        description: "Structural inconsistencies detected while parsing (raw data beyond the file,\r\n" +
                     "overlapping sections, entry point outside any section, undersized headers).\r\n" +
                     "An empty list means the image looks canonical."
    )]
    public string[] Anomalies { get; set; } = [];

    [Seed(
        name: "Rich Header",
        description: "The Rich header is written by the Microsoft linker between the DOS stub and the PE\r\n" +
                     "signature. It records every compiler/linker tool version used to build the image,\r\n" +
                     "XOR-encoded with a key that doubles as a checksum of the DOS header.\r\n" +
                     "Absent in MinGW, Borland and pure .NET (Roslyn) images - which is itself a fingerprint."
    )]
    public RichHeaderModel RichHeader { get; set; } = new();

    [Seed(
        name: "Toolkit Table",
        description: "Iterated tooklit records from Rich header table of given program"
    )]
    public RichHeaderItem[] ToolkitTable { get; set; } = [];

    [Seed(
        name: "Export Directory",
        description: "The export symbol information begins with the export directory table,\r\n " +
                     "which describes the remainder of the export symbol information.\r\n " +
                     "The export directory table contains address information that is used\r\n " +
                     "to resolve imports to the entry points within this image."
    )]
    public PeImageExportDirectory ExportDirectory { get; set; }
    
    [Seed(
        name: "Exports",
        description: "Module entry points"
    )]
    public ExportFunction[] Exports { get; set; } = [];
    
    [Seed(
        name: "Static Imports",
        description: "The import directory table contains address information\r\n" +
                     "that is used to resolve fixup references to the entry points within a DLL image. \r\n" +
                     "The import directory table consists of an array of import directory entries, " +
                     "one entry for each DLL to which the image refers.\r\n" +
                     "The last directory entry is empty, which indicates the end of the directory table.\r\n"
    )]
    public ImportedFunction[] Imports { get; set; } = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        PeDumpManager dumpManager = new(filePath);
        
        PeExportsManager exportsManager = new(dumpManager.ImageDetails, filePath);
        PeImportsManager importsManager = new(dumpManager.ImageDetails, filePath);
        
        exportsManager.Dump();
        importsManager.Dump();
        
        FileHeader = dumpManager.FileHeader;
        OptionalHeader = dumpManager.OptionalHeader;
        Directories = dumpManager.Directories;
        Sections = dumpManager.PeSections;
        Anomalies = [..dumpManager.Anomalies];
        RichHeader = dumpManager.RichHeader;
        ToolkitTable = [..dumpManager.RichHeader.Items];
        ExportDirectory = exportsManager.ExportTable.ExportDirectory;
        Exports = exportsManager.ExportTable.Functions.ToArray();
        Imports = importsManager.ImportTable.Modules
            .SelectMany(x => x.Functions)
            .ToArray();
        
        // clrManager.Dump();
    });

    public string Name => "Dump Portable Executable";
}