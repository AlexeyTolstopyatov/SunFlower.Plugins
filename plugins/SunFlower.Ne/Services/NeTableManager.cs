using System.Runtime.Serialization;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;

namespace SunFlower.Ne.Services;

public class NeTableManager
{
    public class ProgramSummary(
        string cpu,
        Version link,
        string os,
        Version osVersion,
        int segments,
        int bundles,
        int exports,
        int imports)
    {
        public string Cpu = cpu;
        public Version Link = link;
        public string Os = os;
        public Version OsVersion = osVersion;
       
        public int Segments = segments;
        public int Bundles = bundles;
        public int Exports = exports;
        public int Imports = imports;
    }
    public class Program
    {
        public string Runs = string.Empty;
        public long CodeOffset;
        public long StackOffset;
        public long DataOffset;
        public uint Heap;
        public uint Stack;
    }
    
    private readonly NeDumpManager _manager;
    public NeTableManager(NeDumpManager manager)
    {
        _manager = manager;
        CommonCharacteristics();
        MakeProgramDetails();
        
        NestedDataRegions.Add(new MzStructVisualizer(manager.MzHeader).ToRegion());
        NestedDataRegions.Add(new NeStructVisualizer(manager.NeHeader).ToRegion());
        NestedDataRegions.Add(new NeSegmentsVisualizer(manager.Segments).ToRegion());
        foreach (var segment in manager.Segments.Where(s => s.Relocations.Count > 0).Distinct())
        {
            NestedDataRegions.Add(new NeSegmentRelocationsVisualization(segment).ToRegion());
        }
        var index = 1;
        foreach (var bundle in _manager.EntryBundles)
        {
            NestedDataRegions.Add(new NeEntryBundleVisualizer(bundle, index).ToRegion());
            ++index;
        }
        
        if (manager.ResidentNames.Count > 0)
            NestedDataRegions.Add(new NeNamesVisualizer(manager.ResidentNames, true).ToRegion());
        if (manager.NonResidentNames.Count > 0)
            NestedDataRegions.Add(new NeNamesVisualizer(manager.NonResidentNames, false).ToRegion());
        if (manager.ModuleReferences.Count > 0)
            NestedDataRegions.Add(new NeModuleReferencesVisualizer(manager.ModuleReferences).ToRegion());
        if (manager.ImportModels.Count > 0)
            NestedDataRegions.Add(new NeImportsVisualizer(manager.ImportModels).ToRegion());
        
    }

    public List<Region> MainRegions { get; private set; } = [];
    public string[] Characteristics { get; private set; } = [];
    public List<Region> NestedDataRegions { get; } = [];

    private (long code, long stack, long data) ForProtectedMode()
    {
        var cs = _manager.NeHeader.NE_CsIp >> 16;
        var ip = _manager.NeHeader.NE_CsIp & 0xFFFF;
        var ss = _manager.NeHeader.NE_SsSp >> 16;
        var sp = _manager.NeHeader.NE_SsSp & 0xFFFF;
        
        var shift = _manager.NeHeader.NE_Alignment == 0 
            ? 1 << 9 
            : 1 << _manager.NeHeader.NE_Alignment;
        var codeOffset = cs <= _manager.Segments.Count && cs != 0 
            ? _manager.Segments[(int)(cs - 1)].FileOffset * shift + ip
            : 0;
        var bssOffset = ss <= _manager.Segments.Count && ss != 0 
            ? _manager.Segments[(int)ss - 1].FileOffset * shift + sp
            : 0;
        var dataOffset = _manager.NeHeader.NE_AutoSegment != 0 
            ? _manager.Segments[_manager.NeHeader.NE_AutoSegment - 1].FileOffset * shift
            : _manager.Segments.First(x => (x.Flags & 0x007) == 1).FileOffset * shift;
        
        return (codeOffset, bssOffset, dataOffset);
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
    
    private void CommonCharacteristics()
    {
        var os = _manager.NeHeader.NE_OS switch
        {
            0x0 => "Any OS supported", // set for *.FON. means "any OS supported"  
            0x1 => "OS/2",
            0x2 => "Windows/286",
            0x3 => "DOS/4",
            0x4 => "Windows/386",
            0x5 => "BoSS",
            _ => $"Unknown 0x{_manager.NeHeader.NE_OS:X}" // <-- really don't know how handle it
        };

        var cpu = _manager.NeHeader.NE_Flags switch
        {
            var f when (f & 0x4) != 0 => "Intel 8086",
            var f when (f & 0x5) != 0 => "Intel 80286",
            var f when (f & 0x6) != 0 => "Intel 80386",
            var f when (f & 0x7) != 0 => "Intel 8087",
            _ => "Not specified"
        };
        var common = new ProgramSummary(
            os: os,
            cpu: cpu,
            link: new Version(_manager.NeHeader.NE_LinkerVersion, _manager.NeHeader.NE_LinkerRevision),
            osVersion: new Version(_manager.NeHeader.NE_WindowsVersionMajor, _manager.NeHeader.NE_WindowsVersionMinor),
            segments: _manager.Segments.Count,
            bundles: _manager.EntryBundles.Count,
            exports: _manager.NonResidentNames.Count + _manager.ResidentNames.Count,
            imports: _manager.ImportModels.Count
        );
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
                Runs = "Protected mode", 
                Heap = _manager.NeHeader.NE_Heap, 
                Stack = _manager.NeHeader.NE_Stack, 
                CodeOffset = pCodeOffset, 
                StackOffset = pBssOffset, 
                DataOffset = dataOffset
            }
        ];

        var commonRegion = new Region(
            "About", 
            $"This is shorten properties of `{FlowerReport.SafeString(_manager.ResidentNames[0].String)}`", 
            FlowerReflection.GetNameValueTable(common)
        );
        var programRegion = new Region(
            "Executable images",
            "This table represents possible program images, exactly how it could be run from Win16-OS/2 and DOS environments", 
            FlowerReflection.ListToDataTable(programs)
        );
        MainRegions.Add(commonRegion);
        MainRegions.Add(programRegion);
    }
    
    private void MakeProgramDetails()
    {
        List<string> md = [];
        var p = _manager.NeHeader.NE_Flags;
        md.Add($"## Program Flags\n");
        md.Add("### How data is handled?\n");
        
        if ((p & 0x0000) != 0) md.Add(" - `NO_AUTODATA`");
        if ((p & 0x0002) != 0) md.Add(" - `SINGLE_DATA` (shared among instances of the same program)");
        if ((p & 0x2000) != 0) md.Add(" - `MULTIPLE_DATA` (separate for each instance of the same program)");
        
        md.Add("### How application runs?\n");
        if ((p & 0x0008) != 0) md.Add(" - `PROTECTED_MODE_ONLY`");
        if ((p & 0x0004) != 0) md.Add(" - `GLOBAL_INIT` - (global initialization)");
        
        md.Add("### Extra details?\n");
        if ((p & 0x2000) != 0) md.Add(" - `LINK_ERR` - (module has errors after linkage. Don't try to run it)");
        if ((p & 0x8000) != 0) md.Add(" - `LIB_MODULE` (dynamically linked module)");
        
        md.Add("## Application Flags");
        md.Add("This block (field) tells how windowing or not windowing wants to run");
        
        var a = _manager.NeHeader.NE_Flags;
        
        if ((a & 0x0080) != 0) md.Add(" - `OS2_FAMILY` (OS/2 family application. You can see OS/2 flags section)");
        if ((a & 0x0020) != 0) md.Add(" - `IMAGE_ERR` (OS doesn't want that you run it).");
        if ((a & 0x0040) != 0) md.Add(" - `NON_CONFORM` (nonconforming program)");
        
        if (_manager.NeHeader.NE_FlagOthers != 0)
        {
            md.Add("## OS/2 Flags\n");
            md.Add("Sunflower plugin shows this section if `e_flagothers` not zero. But I also suppose if appflags has `OS2_FAMILY`" +
                   " or `e_os` equals 0x1, what means OS/2 - you can read this section.");
            var o = _manager.NeHeader.NE_FlagOthers;
            if ((o & 0x0001) != 0) md.Add(" - `LONG_NAMES` (avoid FAT rule 8.3 convertion)");
            if ((o & 0x0002) != 0) md.Add(" - `PROTECTED_MODE` (OS/2 2.0+ protected mode application)");
            if ((o & 0x0004) != 0) md.Add(" - `PROP_FONTS` (proportional fonts)");
            if ((o & 0x0008) != 0) md.Add(" - `GANGLOAD_AREA`");
        }
        
        Characteristics = md.ToArray();
    }
}