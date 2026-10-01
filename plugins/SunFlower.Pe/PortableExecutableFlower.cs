//
// CoffeeLake (C) 2026-*
// 
// The PortableExecutableFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;
using SunFlower.Pe.Services;

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
                     "the PE loader looks for specific information provided by that header to be able to load and run the executable.\n" +
                     "It’s called the optional header because some file types like object files don’t have it, \r\n" +
                     "however this header is essential for image files."
    )]
    public PeOptionalHeader OptionalHeader { get; set; }

    [Seed(
        name: "Data Directory Table",
        description: "Data Directories are the pieces of data located somewhere in any section of the PE."
    )] 
    public PeDirectory[] Directories { get; set; } = [];
    
    [Seed(
        name: "Section Table",
        description: "Sections are the containers of the actual data of the executable file,\r\n " +
                     "they occupy the rest of the PE file after the headers, " +
                     "precisely after the section headers."
    )]
    public PeSection[] Sections { get; set; } = [];
    
    [Seed(
        name: "Export Directory",
        description: ""
    )]
    public PeImageExportDirectory ExportDirectory { get; set; }
    
    [Seed(
        name: "Exports",
        description: "Module entry points"
    )]
    public ExportFunction[] Exports { get; set; } = [];
    
    [Seed(
        name: "Static Imports",
        description: ""
    )]
    public ImportedFunction[] Imports { get; set; } = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        PeDumpManager dumpManager = new(filePath);
        dumpManager.Dump();
            
        PeExportsManager exportsManager = new(dumpManager.FileSectionsInfo, filePath);
        PeImportsManager importsManager = new(dumpManager.FileSectionsInfo, filePath);
        
        exportsManager.Dump();
        importsManager.Dump();
        
        FileHeader = dumpManager.FileHeader;
        OptionalHeader = dumpManager.OptionalHeader;
        Directories = dumpManager.PeDirectories;
        Sections = dumpManager.PeSections;
        ExportDirectory = exportsManager.ExportTableModel.ExportDirectory;
        Exports = exportsManager.ExportTableModel.Functions.ToArray();
        Imports = importsManager.ImportTableModel.Modules
            .SelectMany(x => x.Functions)
            .ToArray();
        
        // clrManager.Dump();
    });

    public string Name => "Dump Portable Executable";
}