//
// CoffeeLake (C) 2026-*
// 
// This flower extracts all Linear executable layout and presents it
// in data types/structures. Supports
//      - LE header
//      - Objects
//      - Module Pages
//      - Entry table
//      - Non/Resident names
//      - Fixup pages
//      - Fixup records
//      - Import fixup symbols
//      - VxD header
//      - VxD VS_VERSION_INFO block
//      - VxD DDB block
// Not supports an extraction of resources from resource objects!
// Uses to correctly disassembly a program module image
//
// The LinearExecutableFlower.cs demonstrates a SunFlower 5 plugins API which makes  
// a focus on the subject details, not sunflower data types and internal loader processes  
// 
// It might be the same with WPF manifest files which declares basics of Windowed application
// (.NET assembly) to embed them into Win32 resources but seemed to be more comfortable
// than IFlowerSeed API.
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using SunFlower.Le.Services;
using Entry = SunFlower.Le.Models.Entry;
using Object = SunFlower.Le.Headers.Le.Object;

namespace SunFlower.Le;

[Flower(FlowerTarget.Data)]
[FlowerVersionContract(5, 0, 0)]
public class LinearExecutableFlower : IFlower
{
    [Seed(
        name: "Linear Executable Header",
        description: "This program might contain mixed 16 and 32 bit code. \n" +
                     "This object module format was used by Microsoft Windows/386 and lately by DOS extenders"
    )]
    public LeHeader Header { get; private set; }

    [Seed(
        name: "Windows Device Driver",
        description: "VxD is the device driver model used in Microsoft Windows/386 2.x," +
                     "the 386 enhanced mode of Windows 3.x, Windows 9x,\r\n and to some extent also by the Novell DOS 7," +
                     "OpenDOS 7.01, and DR-DOS 7.02+ multitask manager (TASKMGR). " +
                     "VxDs have access to the memory of the kernel and all running processes\r\n, " +
                     "as well as raw access to the hardware. \r\n" +
                     "Starting with Windows 98, Windows Driver Model was the recommended driver model to write drivers for,\r\n " +
                     "with the VxD driver model still being supported for backward compatibility\r\n, until Windows Me.\r\n"
    )]
    public VxdHeader VxdHeader { get; private set; }

    [Seed(
        name: "Device Description Block",
        description: "The device declaration block describes the virtual device to the VMM. \r\n" +
                     "It provides a VxD mnemonic, usually a somewhat descriptive title using V as the prefix and D \r\n as " +
                     "the suffix, such as VXFERD, suggesting a virtual transfer driver. \r\n" +
                     "It also provides a major and minor version, the main control procedure, \r\n" +
                     "the device ID number, the initialization order, \r\n" +
                     "and control procedures for the V86 or Protected-Mode (PM) API:\r\n"
    )]
    public VxdDescriptionBlock VxdDescriptionBlock { get; private set; }

    [Seed(
        name: "Objects",
        description: "Objects defines a sections of code and data what require to be placed" +
                     "in specific memory allocation while module is loading."
    )]
    public Object[] Objects { get; private set; } = [];

    [Seed(
        name: "Pages",
        description: "The Object page table provides information about a logical \n" +
                     "page in an object. A logical page may be an enumerated \n" +
                     "page, a pseudo page or an iterated page. "
    )]
    public Headers.Le.ObjectPage[] Pages { get; private set; } = [];

    [Seed(
        name: "Entry Points", 
        description: "Table of all entry points from read entry bundles"
    )]
    public Entry[] Entries { get; private set; } = [];

    [Seed(
        name: "Non-Resident Names",
        description: "Non-resident  names are not kept in memory \r\n" +
                     "and are read from the `EXE` file when a dynamic link reference\r\n" +
                     "is made. Exported entry point names that are infrequently\r\n" +
                     "dynamicaly linked to by name or are commonly referenced\r\n" +
                     "by ordinal number should be placed in the \n non-resident name table.\r\n" +
                     "The trade off made for references by name is performance vs memory usage.\r\n\r\n"
    )]
    public ExportRecord[] NonResidentNames { get; private set; } = [];

    [Seed(
        name: "Resident Names",
        description: "The resident name table is kept resident in system memory \r\n" +
                     "while the module is loaded. It is intended to contain  the \r\n " +
                     "exported entry point names that are frequently dynamicaly \r\n " +
                     "linked to by name."
    )]
    public ExportRecord[] ResidentNames { get; private set; } = [];

    [Seed(
        name: "Runtime Imports",
        description: "Resolved symbols from relocation records which are tells operating system \n" +
                     "to load extern code from another module during this module run-time"
    )]
    public ImportRecord[] ImportNames { get; private set; } = [];

    public async Task CreateAsync(string filePath)
    {
        await Task.Run(() =>
        {
            LeDumpManager manager = new(filePath);
            Header = manager.LeHeader;
            VxdHeader = manager.VxdHeader;
            VxdDescriptionBlock = manager.VxdDescriptionBlock;
            Objects = manager.Objects;
            Pages = manager.Pages.Select(p => p.Page).ToArray();
            
            Entries = manager.EntryBundles
                .SelectMany(x => x.Entries)
                .Where(x => x.Type != EntryBundleType.Unused)
                .Select(x =>
            {
                switch (x.Type)
                {
                    case EntryBundleType._16Bit:
                        var _16 = (Entry16Bit)x;

                        return new Entry(
                            ordinal: _16.Ordinal,
                            name: _16.EntryName,
                            obj: _16.ObjectNumber,
                            offset: _16.Offset,
                            bundleType: x.Type,
                            entryType: _16.EntryType
                        );
                    case EntryBundleType._32Bit:
                        var _32 = (Entry32Bit)x;
                        return new Entry(
                            ordinal: _32.Ordinal,
                            name: _32.EntryName,
                            obj: _32.ObjectNumber,
                            offset: (int)_32.Offset,
                            bundleType: x.Type,
                            entryType: _32.EntryType
                        );
                    case EntryBundleType._286CallGate:
                        var gate = (Entry286CallGate)x;
                        return new Entry(
                            ordinal: gate.Ordinal,
                            name: gate.EntryName,
                            obj: gate.ObjectNumber,
                            offset: gate.Offset,
                            bundleType: x.Type,
                            entryType: gate.EntryType
                        );
                    case EntryBundleType.Forwarder:
                        var fwd = (EntryForwarder)x;
                        return new Entry(
                            ordinal: fwd.Ordinal,
                            name: fwd.EntryName,
                            obj: (int)fwd.ModuleOrdinal,
                            offset: (int)fwd.OffsetOrOrdinal,
                            bundleType: x.Type,
                            entryType: (fwd.Flags & 0x01) != 0 ? "Ordinal" : "Name"
                        );
                    case EntryBundleType.Unused:
                    default:
                        return new Entry(
                            ordinal: 0,
                            name: string.Empty,
                            obj: 0,
                            offset: 0,
                            bundleType: EntryBundleType.Unused,
                            entryType: "?"
                        );
                
                }
            }).ToArray();
            
            ResidentNames = manager.ResidentNames;
            NonResidentNames = manager.NonResidentNames;
            ImportNames = manager.ImportRecords;
        });
    }

    public string Name => "Dump MS OS/2 Linear Executable";
}