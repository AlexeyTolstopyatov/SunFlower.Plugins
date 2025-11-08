using System.Data;
using System.Text;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Services;

public class LxTableManager
{
    private LxDumpManager _manager;
    
    public List<Region> ObjectRegions { get; set; } = [];
    public List<Region> EntryTableRegions { get; set; } = [];
    public List<Region> NamesRegions { get; set; } = [];
    public List<string> Characteristics { get; set; } = [];
    public string[] ImportedNames { get; set; } = [];
    public string[] ImportedProcedures { get; set; } = [];
    public List<DataTable> Headers { get; set; } = [];
    
    public LxTableManager(LxDumpManager manager)
    {
        _manager = manager;
        // list of init queue
        MakeCharacteristics();
        MakeHeaders();
        MakeObjectTables();
        MakeNames();
        MakeEntryTable();
        MakeFixupRecords();
        MakeImports();
    }

    private void MakeFixupRecords()
    {
        var reg = new Region("## Fixup Records", "???", FlowerReflection.ListToDataTable(_manager.FixupRecords));
        
        ObjectRegions.Add(reg);
    }

    private void MakeImports()
    {
        var reg = new Region("## Imports", "??", FlowerReflection.ListToDataTable(_manager.ImportRecords));
        
        NamesRegions.Add(reg);
    }
    private void MakeHeaders()
    {
        List<DataTable> tables =
        [
            MakeMzHeader(_manager.MzHeader),
            MakeLxHeader(_manager.LxHeader)
        ];
        
        Headers = tables;
    }

    private DataTable MakeMzHeader(MzHeader mz)
    {
        return FlowerReflection.GetNameValueTable(mz);
    }

    private DataTable MakeLxHeader(LxHeader header)
    {
        return FlowerReflection.GetNameValueTable(header);
    }

    private static void AddRow(ref DataTable table, object a, object b)
    {
        table.Rows.Add(a, b);
    }
    private void MakeObjectTables()
    {
        DataTable objectsTable = new("Objects Table");
        const string objectHead = "### Objects Table";
        const string objectContent = "Meaning of objects in ObjectTable are the same with sections of modern executable binaries for a first time. " +
                                     "The number of entries in the Object Table is given by the # Objects in Module field in the linear " +
                                     "\nEXE header. Entries in the Object Table are numbered starting from one.";
        
        objectsTable.Columns.Add("#");
        objectsTable.Columns.Add("Name:s");
        objectsTable.Columns.Add("VirtualSize:4");
        objectsTable.Columns.Add("RelBase:4");
        objectsTable.Columns.Add("FlagsMask:4");
        objectsTable.Columns.Add("PageMapIndex:4");
        objectsTable.Columns.Add("PageMapEntries:4");
        objectsTable.Columns.Add("Unknown:4");
        objectsTable.Columns.Add("Flags:s");

        var counter = 1;
        foreach (var table in _manager.Objects)
        {
            var text = table
                .ObjectFlags
                .Aggregate("", (current, s) => current + $"`{s}` ");
            var name = SunFlower.Le.Headers.Lx.Object.GetSuggestedNameByPermissions(table);
            
            objectsTable.Rows.Add(
                counter,
                name,
                "0x" + table.VirtualSegmentSize.ToString("X8"),
                "0x" + table.RelocationBaseAddress.ToString("X8"),
                "0x" + table.ObjectFlagsMask.ToString("X8"),
                "0x" + table.PageMapIndex.ToString("X8"),
                "0x" + table.PageMapEntries.ToString("X8"),
                table.Unknown.ToString("X8"),
                text
            );

            counter++;
        }
        ObjectRegions.Add(new Region(objectHead, objectContent, objectsTable));

        const string objectPageHead = "### Object Pages";
        const string objectPageContent = "The object page table specifies where in the EXE file a page can be found " +
                                         "for a given object and specifies per-page attributes. " +
                                         "The object table entries are ordered by logical page in the object table. " +
                                         "In other words the object table entries are sorted based on the object page table index value. ";
        
        ObjectRegions.Add(new Region(objectPageHead, objectPageContent, FlowerReflection.ListToDataTable(_manager.Objects)));
    }
    private void MakeNames()
    {
        var residentHeader = "### Resident Names Table";
        var notResidentHeader = "### NonResident Names Table";

        var residentContent = "The resident name table is kept resident in system memory while the module is loaded. It is intended to contain the exported entry point names that are frequently dynamicaly linked to by name.";
        var notResidentContent = "Non-resident names are not kept in memory and are read from the EXE file when a dynamic link reference is made.";
        
        NamesRegions.Add(new Region(residentHeader, residentContent, FlowerReflection.ListToDataTable(_manager.ResidentNames)));
        NamesRegions.Add(new Region(notResidentHeader, notResidentContent, FlowerReflection.ListToDataTable(_manager.NonResidentNames)));
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
                        Columns = { "Ordinal#:2", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry16Bit>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
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
                        Columns = { "Ordinal#:2", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry32Bit>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
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
                        Columns = { "Ordinal#:2", "Offset:2", "Entry:s", "Flags:1", "ObjectOffsets:s", "CallGate:2" }
                    };
                    
                    foreach (var unpacked in bundle.Entries.Cast<Entry286CallGate>())
                    {
                        entries.Rows.Add(
                            "@"  + entryCounter,
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
                        Columns = { "Ordinal#:2", "@Module:4", "@Offset:4", "Reserved:2" }
                    };
                    foreach (var unpacked in bundle.Entries.Cast<EntryForwarder>())
                    {
                        entries.Rows.Add(
                            "@" + entryCounter,
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