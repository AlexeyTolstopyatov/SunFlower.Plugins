using System.Data;
using System.Text;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using SunFlower.Le.Visualizers;

namespace SunFlower.Le.Services;

public class LxTableManager
{
    internal class ProgramSummary(
        string cpu,
        Version link,
        string os,
        int objects,
        int bundles,
        int exports,
        int imports)
    {
        public string Cpu = cpu;
        public Version Link = link;
        public string Os = os;
        
        public int Objects = objects;
        public int Bundles = bundles;
        public int Exports = exports;
        public int Imports = imports;
    }
    internal class Program
    {
        public string Runs = string.Empty;
        public long CodeOffset;
        public long StackOffset;
        public long DataOffset;
        public uint Heap;
        public uint Stack;
    }

    private LxDumpManager _manager;
    
    public List<Region> ObjectRegions { get; } = [];
    public List<Region> EntryTableRegions { get; } = [];
    public List<Region> NamesRegions { get; } = [];
    public List<string> Characteristics { get; private set; } = [];
    public List<Region> Headers { get; } = [];
    public List<Region> MainRegions { get; private set; } = [];
    
    public LxTableManager(LxDumpManager manager)
    {
        _manager = manager;
        // list of init queue
        CollectMain();
        MakeCharacteristics();
        Headers.Add(new LxHeaderVisualizer(_manager.LxHeader).ToRegion());
        ObjectRegions.Add(new LxObjectPagesVisualizer(_manager.Pages).ToRegion());
        ObjectRegions.Add(new LxObjectTableVisualizer(_manager.Objects).ToRegion());
        MakeEntryTable();
        ObjectRegions.Add(new FixupPagesVisualizer(_manager.FixupPageOffsets).ToRegion());
        MakeFixupRecords();
        MakeImports();
        NamesRegions.Add(new ResidentNamesVisualizer(_manager.ResidentNames).ToRegion());
        NamesRegions.Add(new NonResidentNamesVisualizer(_manager.NonResidentNames).ToRegion());
    }
    private (long code, long stack) ForRealMode()
    {
        var codeOffset = _manager.MzHeader.cs != 0
            ? _manager.MzHeader.e_pars * 0x10 + _manager.MzHeader.cs * 0x10 + _manager.MzHeader.ip
            : 0;
        var bssOffset = _manager.MzHeader.ss != 0
            ? _manager.MzHeader.e_pars * 0x10 + _manager.MzHeader.ss * 0x10 + _manager.MzHeader.sp
            : 0;
        return (codeOffset, bssOffset);
    }

    private long GetOffset(int objectIndex, uint rva)
    {
        if (objectIndex < 1 || objectIndex > _manager.Objects.Count)
            return 0;

        var obj = _manager.Objects[objectIndex - 1];
        var pageSize = _manager.LxHeader.e32_pagesize;
        var pageShift = _manager.LxHeader.e32_pageshift;

        // Which object page 
        var pageIndexInObject = rva / pageSize;
        if (pageIndexInObject >= obj.PageMapEntries)
            return 0; // out of bounds

        // Global# into Object Page Table
        var globalPageIdx = obj.PageMapIndex + pageIndexInObject;
        if (globalPageIdx < 1 || globalPageIdx > _manager.Pages.Count)
            return 0;

        var page = _manager.Pages[(int)globalPageIdx - 1];

        // Physical... still exists?
        var flags = page.Flags;
        if ((flags & 0x03) == 0x02 || (flags & 0x03) == 0x03)
            return 0; // invalid/iterated page

        var pageFileOffset = _manager.LxHeader.e32_mpages + // DataPagesOffset
                             ((long)page.PageOffset << (int)pageShift);

        // Offset inside the page
        var offsetInPage = rva % pageSize;
        return pageFileOffset + offsetInPage;
    }
    private (long code, long stack, long data) ForProtectedMode()
    {
        return (
            GetOffset((int)_manager.LxHeader.e32_startobj, _manager.LxHeader.e32_eip), 
            GetOffset((int)_manager.LxHeader.e32_stackobj, _manager.LxHeader.e32_esp),
            GetOffset((int)_manager.LxHeader.e32_autodata, 0)
        );
    }
    private void CollectMain()
    {
        var os = GetOsType(_manager.LxHeader.e32_os);
        var cpu = GetCpuType(_manager.LxHeader.e32_cpu);
        var ver = new Version((int)(_manager.LxHeader.e32_ver >> 16), (int)(_manager.LxHeader.e32_ver & 0xFFFF));
        var objects = _manager.Objects.Count;
        var exports = _manager.ResidentNames.Count + _manager.NonResidentNames.Count;
        var bundles = _manager.EntryBundles.Count;
        var imports = _manager.ImportRecords.Count;
        var common = new ProgramSummary(
            cpu,
            ver,
            os,
            objects,
            bundles,
            exports,
            imports
        );

        var commonRegion = new Region(
            "About",
            $"This is shortened properties of {FlowerReport.SafeString(_manager.ResidentNames[0].String)}",
            FlowerReflection.GetNameValueTable(common));
        var (rCodeOffset, rBssOffset) = ForRealMode();
        var (pCodeOffset, pBssOffset, dataOffset) = ForProtectedMode();
        
        Program[] programs =
        [   
            new()
            {
                Runs = "Real mode", 
                CodeOffset = rCodeOffset, 
                StackOffset = rBssOffset
            },
            new()
            {
                Runs = "V86/Protected mode", 
                Heap = _manager.LxHeader.e32_heapsize, 
                Stack = _manager.LxHeader.e32_stacksize, 
                CodeOffset = pCodeOffset, 
                StackOffset = pBssOffset, 
                DataOffset = dataOffset
            }
        ];
        var programRegion = new Region(
            "Executable images",
            "This table represents possible program images, exactly how it could be run from Win16-OS/2 and DOS environments", 
            FlowerReflection.ListToDataTable(programs)
        );
        MainRegions.Add(commonRegion);
        MainRegions.Add(programRegion);
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
            "Internal Fixups | Common data", 
            "Every relocation record has same columns what describe next uniqe information", FlowerReflection.ListToDataTable(internalFixups)));
        ObjectRegions.Add(new Region(
            "Internal Fixups | Target data",
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
            "Import Fixups | Common data",
            "Importing procedures fixups",
            FlowerReflection.ListToDataTable(importFixups)));
        ObjectRegions.Add(new Region(
            "Import Fixups | Target data",
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
            "Fixups via EntryTable | Common data",
            "IBM documentation tells, this record is a pointer to entry table of _current module_",
            FlowerReflection.ListToDataTable(entFixups)
            ));
        ObjectRegions.Add(new Region(
            "Fixups via EntryTable | Target data",
            "This is a list of indexes/ordinals of entries in entry table of current module",
            FlowerReflection.ListToDataTable(entFixupData)
            ));
    }
    private void MakeImports()
    {
        var reg = new Region(
            "Imports",
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
            var head = $"EntryTable Bundle #{bundleCounter}";
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
                            bundle.ObjectNumber == 0 ? "[FILE]" : "[VIRTUAL]"
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
                            bundle.ObjectNumber == 0 ? "[FILE]" : "[VIRTUAL]"
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
                            bundle.ObjectNumber == 0 ? "[FILE]" : "[VIRTUAL]",
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
                    entries = new DataTable();
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
        md.Add($"Project Name: {name}");
        md.Add($"Description: \"{description}\"");
        
        md.Add($"Resolved \"{_manager.ResidentNames[0].String}\" module flags:");
        
        md.AddRange(GetModuleFlags(_manager.LxHeader.e32_mflags).Select(flag => $" - `{flag}`"));

        if (_manager.LxHeader.e32_magic is 0x454c or 0x4c45)
            md.Add("> ![WARNING]\r\n> Signature of FLAT EXEC header is `LE`. This FLAT EXEC binary contains **16 and 32-bit code** You have a risk of corrupted bytes-interpretation.");
        
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