//
// CoffeeLake (C) 2026-*
// 
// The NewExecutableFlower.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;
using SunFlower.Ne.Services;

namespace SunFlower.Ne;

[Flower(FlowerTarget.Data)]
[FlowerVersionContract(5, 0, 0)]
public class NewExecutableFlower : IFlower
{
    [Seed(
        name: "New Executable Header",
        description: "The segmented New Executable is a deprecated module format\r\n" +
                     "which was used in 16-bit operating systems like \r\n" +
                     "Multitasking MS-DOS 4.0, Microsoft Windows 1.x, 2.x, 3.x, IBM OS/2 1.x" +
                     "was introduced in 1985."
    )]
    public NeHeader Header { get; private set; }
    [Seed(
        name: "Segment Table",
        description: "Segment table stores information about program\r\n" +
                     "code and data segments which will be loaded in memory\r\n"
    )]
    public SegmentModel[] Segments { get; private set; } = [];
    [Seed(
        name: "Entry Table",
        description: "This table contains bundles of entry-point definitions.\r\n" +
                     "Bundling is done to save space in the entry table.\r\n" +
                     "The entry table is accessed by an ordinal value.\r\n"
    )]
    public EntryTableModel[] Entries { get; private set; } = [];
    
    [Seed(
        name: "Resident Names",
        description: "The resident-name table follows the resource table, \r\n" +
                     "and contains this module's name string and resident exported procedure name strings. \r\n" +
                     "The first string in this table is this module's name."
    )]
    public Name[] ResidentNames { get; private set; } = [];
    [Seed(
        name: "Non Resident Names",
        description: "The nonresident-name table follows the entry table and contains a module description \r\n" +
                     "and nonresident exported procedure name strings. \r\n" +
                     "The first string in this table is a module description."
    )]
    public Name[] NonResidentNames { get; private set; } = [];
    [Seed(
        name: "Runtime Imports",
        description: "Records in this table are runtime import relocations which operating system" +
                     "needs to fix up while module is loading."
    )]
    public Import[] Imports { get; private set; } = [];
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        var manager = new NeDumpManager(filePath);

        Header = manager.NeHeader;
        Segments = manager.Segments.ToArray();
        Entries = manager.EntryBundles
            .SelectMany(x => x.EntryPoints)
            .Where(x => x.Type != "Unused")
            .ToArray();
        ResidentNames = manager.ResidentNames.ToArray();
        NonResidentNames = manager.NonResidentNames.ToArray();
        Imports = manager.ImportModels
            .SelectMany(x => x.Value)
            .ToArray();
    });
    
    public string Name => "Dump New Executable (NE)";
}