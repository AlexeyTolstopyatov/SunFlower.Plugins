using System.Data;
using System.Text;
using Microsoft.Win32;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using SunFlower.Le.Headers.Lx;
using SunFlower.Le.Visualizers;

namespace SunFlower.Le.Services;

public class LxTableManager
{
    private LxDumpManager _manager;
    
    public List<Region> ObjectRegions { get; } = [];
    public List<Region> EntryTableRegions { get; } = [];
    public List<Region> NamesRegions { get; } = [];
    public List<string> Characteristics { get; private set; } = [];
    public List<Region> Headers { get; } = [];
    
    public LxTableManager(LxDumpManager manager)
    {
        _manager = manager;
        // list of init queue
        MakeCharacteristics();
        Headers.Add(new HeaderVisualizer(_manager.LxHeader).ToRegion());
        ObjectRegions.Add(new ObjectPagesVisualizer(_manager.Pages).ToRegion());
        ObjectRegions.Add(new ObjectTableVisualizer(_manager.Objects).ToRegion());
        MakeEntryTable();
        ObjectRegions.Add(new FixupPagesVisualizer(_manager.FixupPageOffsets).ToRegion());
        MakeFixupRecords();
        MakeImports();
        NamesRegions.Add(new ResidentNamesVisualizer(_manager.ResidentNames).ToRegion());
        NamesRegions.Add(new NonResidentNamesVisualizer(_manager.NonResidentNames).ToRegion());
    }

    private void MakeFixupRecords()
    {
        var internalFixups = _manager.FixupRecords
            .Where(t => t.TargetData is FixupTargetInternal)
            .ToList();

        var internalFixupsData = internalFixups
            .Select(t => (FixupTargetInternal)t.TargetData)
            .ToList();
        
        ObjectRegions.Add(new Region(
            "### Internal Fixups | Common data", 
            "Every relocation record has same columns what describe next uniqe information", FlowerReflection.ListToDataTable(internalFixups)));
        ObjectRegions.Add(new Region(
            "### Internal Fixups | Target data",
            "Those blocks fully depend on previous headers details",
            FlowerReflection.ListToDataTable(internalFixupsData)
            ));

        var importFixups = _manager.FixupRecords
            .Where(t => t.TargetFlags is 0x02 or 0x01)
            .ToList();
        var importFixupsData = importFixups
            .Select(t => t.TargetData)
            .ToList();
        
        // those tables are same with fields
        var dt = new DataTable
        {
            Columns = { "Module#:2", "Procedure@:4/Procedure Offset:4" }
        };

        foreach (var rec in importFixupsData)
        {
            switch (rec)
            {
                case FixupTargetImportedName i:
                    dt.Rows.Add($"0x{i.ModuleOrdinal:X4}", $"0x{i.ProcedureNameOffset:X8}");
                    break;
                case FixupTargetImportedOrdinal o:
                    dt.Rows.Add($"0x{o.ModuleOrdinal:X4}", $"@{o.ImportOrdinal}");
                    break;
            }
        }
        ObjectRegions.Add(new Region(
            "### Import Fixups | Common data",
            "Importing procedures fixups",
            FlowerReflection.ListToDataTable(importFixups)));
        ObjectRegions.Add(new Region(
            "### Import Fixups | Target data",
            "Importing procedures unique data",
            dt));
        var entFixups = _manager.FixupRecords
            .Where(t => t.TargetData is FixupTargetEntryTable)
            .ToList();
        var entFixupData = entFixups
            .Select(t => (FixupTargetEntryTable)t.TargetData)
            .ToList();
        ObjectRegions.Add(
            new Region(
            "### Fixups via EntryTable | Common data",
            "IBM documentation tells, this record is a pointer to entry table of _current module_",
            FlowerReflection.ListToDataTable(entFixups)
            ));
        ObjectRegions.Add(new Region(
            "### Fixups via EntryTable | Target data",
            "This is a list of indexes/ordinals of entries in entry table of current module",
            FlowerReflection.ListToDataTable(entFixupData)
            ));
    }

    private void MakeImports()
    {
        var reg = new Region(
            "### Imports",
            @"All imports resolved using fixup records table for this module.", 
            FlowerReflection.ListToDataTable(_manager.ImportRecords));
        
        NamesRegions.Add(reg);
    }
    private void MakeEntryTable()
    {
        var bundleCounter = 1;
        var entryCounter = 1;
        foreach (var bundle in _manager.EntryBundles)
        {
            var head = $"### EntryTable Bundle #{bundleCounter}";
            StringBuilder contentBuilder = new();
            
            contentBuilder.AppendLine(bundle.TypeDescription);
            contentBuilder.AppendLine($" - Type={bundle.TypeString}");
            contentBuilder.AppendLine($" - Entries=`{bundle.Count}`");
            contentBuilder.AppendLine($" - Object#=`{bundle.ObjectNumber}`");
            
            contentBuilder.AppendLine("\r\n");
            contentBuilder.AppendLine($"Rows affected: {bundle.Count}");

            DataTable entries;
            switch (bundle.Type)
            {
                case EntryBundleType._16Bit:
                    entries = new()
                    {
                        Columns = { "Ordinal#:2", "Name:s", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry16Bit>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
                            unpacked.EntryName,
                            "0x" + unpacked.Offset.ToString("X4"),
                            unpacked.EntryType,
                            "0x" + unpacked.Flags.ToString("X2"),
                            unpacked.ObjectOffsets
                        );
                        ++entryCounter;
                    }
                    break;
                case EntryBundleType._32Bit:
                    entries = new()
                    {
                        Columns = { "Ordinal#:2", "Name:s", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry32Bit>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
                            unpacked.EntryName,
                            "0x" + unpacked.Offset.ToString("X8"),
                            unpacked.EntryType,
                            "0x" + unpacked.Flags.ToString("X2"),
                            unpacked.ObjectOffsets
                        );
                        ++entryCounter;
                    }
                    break;
                case EntryBundleType._286CallGate:
                    entries = new()
                    {
                        Columns = { "Ordinal#:2", "Name:s", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s", "CallGate:2" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry286CallGate>())
                    {
                        entries.Rows.Add(
                            "@"  + entryCounter,
                            unpacked.EntryName,
                            "0x" + unpacked.Offset.ToString("X4"),
                            unpacked.EntryType,
                            "0x" + unpacked.Flags.ToString("X2"),
                            unpacked.ObjectOffsets,
                            "0x" + unpacked.CallGateSelector.ToString("X4")
                        );
                        ++entryCounter;
                    }
                    break;
                case EntryBundleType.Forwarder:
                    entries = new()
                    {
                        Columns = { "Ordinal#:2", "Name:s", "Flags:1", "@Module:4", "@Offset:4", "Reserved:2" }
                    };
                    foreach (var unpacked in bundle.Entries.Cast<EntryForwarder>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
                            unpacked.EntryName,
                            "0x" + unpacked.Flags.ToString("X2"),
                            "0x" + unpacked.ModuleOrdinal.ToString("X8"),
                            "0x" + unpacked.OffsetOrOrdinal.ToString("X8"),
                            "0x" + unpacked.Reserved.ToString("X4")
                        );
                        ++entryCounter;
                    }
                    break;
                default:
                    entries = new();
                    entryCounter += bundle.Count;
                    break;
            }
            EntryTableRegions.Add(new Region(head, contentBuilder.ToString(), entries));
            bundleCounter++;
        }
    }
    private void MakeCharacteristics()
    {
        List<string> md = [];
        var description = _manager.NonResidentNames.Count > 0 ? FlowerReport.SafeString(_manager.NonResidentNames[0].String) : "`<missing>`";
        var name = _manager.ResidentNames.Count > 0 ? FlowerReport.SafeString(_manager.ResidentNames[0].String) : "`<name_missing>`";
        md.Add("### Program Header information");
        md.Add($"Project Name: {name}");
        md.Add($"Description: \"{description}\"");
        md.Add("Target CPU: `" + GetCpuType(_manager.LxHeader.e32_cpu) + "`");
        md.Add("Target OS: `" + GetOsType(_manager.LxHeader.e32_os) + "`");
        md.Add($"Module Version: {_manager.LxHeader.e32_ver >> 16}.{_manager.LxHeader.e32_ver & 0xFFFF}");
        
        md.Add($"Resolved \"{_manager.ResidentNames[0].String}\" module flags:");
        foreach (var flag in GetModuleFlags(_manager.LxHeader.e32_mflags))
        {
            md.Add($" - `{flag}`");
        }
        
        if (_manager.LxHeader.e32_magic is 0x454c or 0x4c45)
            md.Add("> ![WARNING]\r\n> Signature of FLAT EXEC header is `LE`. This FLAT EXEC binary contains **16 and 32-bit code** You have a risk of corrupted bytes-interpretation.");
        
        md.Add($"\r\n### {name} Loader requirements");
        md.Add("This summary contains hexadecimal values from FLAT EXEC header.");
        md.Add($" - Heap=`{_manager.LxHeader.e32_heapsize:X}`");
        md.Add($" - Stack={_manager.LxHeader.e32_stacksize:X}");
        md.Add($" - DOS/2 `CS:IP=0x{_manager.MzHeader.cs:X4}:0x{_manager.MzHeader.ip:X4}`");
        md.Add($" - DOS/2 `SS:SP=0x{_manager.MzHeader.ss:X4}:0x{_manager.MzHeader.sp:X4}`");
        
        var cs = _manager.LxHeader.e32_startobj;
        var ip = _manager.LxHeader.e32_eip;
        md.Add($" - OS/2 `CS:EIP=0x{cs:X8}:0x{ip:X8}`"); // <-- handle it
        md.Add($" - OS/2 `SS:ESP=0x{_manager.LxHeader.e32_stackobj:X8}:0x{_manager.LxHeader.e32_esp:X8}`");
        
        md.Add($"> ![TIP]\r\n> Flat EXE Header holds on relative EntryPoint address. EntryPoint stores in [#{cs}](decimal) object with `EIP=0x{ip:X}` offset");
        
        md.Add($"\r\n### {name} Entities summary");
        md.Add("This summary contains decimal values took from FLAT EXEC Header model.");
        md.Add($"1. Number of Objects - `{_manager.LxHeader.e32_objcnt}`");
        md.Add($"2. Number of Importing Modules - `{_manager.LxHeader.e32_impmodcnt}`");
        md.Add($"3. Number of Preload Pages - `{_manager.LxHeader.e32_preload}`");
        md.Add($"4. Number of Automatic Data segments - `{_manager.LxHeader.e32_autodata}`");
        md.Add($"5. Number of Resources - `{_manager.LxHeader.e32_rsrccnt}`");
        md.Add($"6. Number of NonResident names - `{_manager.LxHeader.e32_cbnrestab}`");
        md.Add($"7. Number of Directives - `{_manager.LxHeader.e32_dircnt}`");
        md.Add($"8. Number of Demand Instances - `{_manager.LxHeader.e32_instdemand}`");
        
        Characteristics = md;
    }
    private static string GetCpuType(ushort cpuType)
    {
        return cpuType switch
        {
            LeHeader.LeCpu286 => "Intel 286",
            LeHeader.LeCpu386 => "Intel 386",
            LeHeader.LeCpu486 => "Intel 486",
            LeHeader.LeCpu586 => "Intel Pentium",
            LeHeader.LeCpuI860 => "Intel i860",
            LeHeader.LeCpuN11 => "N11",
            LeHeader.LeCpuR2000 => "MIPS R2000",
            LeHeader.LeCpuR6000 => "MIPS R6000",
            LeHeader.LeCpuR4000 => "MIPS R4000",
            _ => $"Unknown: (0x{cpuType:X4})"
        };
    }
    
    private static string GetOsType(ushort osType)
    {
        return osType switch
        {
            LeHeader.LeOsOs2 => "OS/2",
            LeHeader.LeOsWindows => "Windows/286",
            LeHeader.LeOsDos4 => "DOS 4.x",
            LeHeader.LeOsWin386 => "Windows/386",
            _ => $"Unknown OS (0x{osType:X4})"
        };
    }
    
    private static string[] GetModuleFlags(uint flags)
    {
        var result = new List<string>();

        if ((flags & LeHeader.LeTypeInitPer) != 0)
            result.Add("Initialise per-process library");

        if ((flags & LeHeader.LeTypeIntFixup) != 0)
            result.Add("No internal fixups");

        if ((flags & LeHeader.LeTypeExtFixup) != 0)
            result.Add("No external fixups");

        if ((flags & LeHeader.LeTypeNoLoad) != 0)
            result.Add("Module not loadable");

        if ((flags & LeHeader.LeTypeDll) != 0)
            result.Add("DLL module");

        if (result.Count == 0)
            result.Add("No special flags");

        return result.ToArray();
    }
}