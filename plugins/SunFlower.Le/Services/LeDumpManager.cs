using System.Runtime.InteropServices;
using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using EntryBundle = SunFlower.Le.Headers.EntryBundle;
using ObjectPage = SunFlower.Le.Models.Le.ObjectPage;

namespace SunFlower.Le.Services;

public class LeDumpManager : UnsafeManager
{
    public MzHeader MzHeader { get; }
    public LeHeader LeHeader { get; }
    public List<ExportRecord> ResidentNames { get; }
    public List<ExportRecord> NonResidentNames { get; }
    public List<EntryBundle> EntryBundles { get; }
    public List<Headers.Le.Object> Objects { get; }
    public List<ObjectPage> Pages { get; }
    public List<FixupRecord> FixupRecords { get; }
    public List<FixupPageRecord> FixupPageOffsets { get; }
    public List<ImportRecord> ImportRecords { get; }

    public VxdHeader VxdHeader { get; set; }
    public VxdDescriptionBlock VxdDescriptionBlock { get; set; }
    public VxdResources VxdResources { get; set; }
    
    private readonly uint _offset;

    private uint Offset(uint addr) => _offset + addr;
    
    public LeDumpManager(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        MzHeader = Fill<MzHeader>(reader);
        if (MzHeader.e_sign is not 0x5a4d and 0x4d5a)
            throw new NotSupportedException("Doesn't have MZ magic");

        _offset = MzHeader.e_lfanew;
        stream.Position = MzHeader.e_lfanew;
        
        LeHeader = Fill<LeHeader>(reader);
        if (LeHeader.e32_magic != 0x454c && LeHeader.e32_magic != 0x4c45)
            throw new NotSupportedException("Doesn't have 'LE' magic");
        
        var namesTables = new NamesTablesManager(reader, Offset(LeHeader.e32_restab), LeHeader.e32_nrestab);
        var importNames = new ImportNamesManager(reader, Offset(LeHeader.e32_impmod));
        var entryTable = new EntryTableManager(reader, Offset(LeHeader.e32_enttab), namesTables.ResidentNames, namesTables.NonResidentNames, importNames.ImportingModules, Offset(LeHeader.e32_impproc));
        var objectTable = new LeObjectsManager(reader, Offset(LeHeader.e32_objtab), LeHeader.e32_objcnt);
        var pagesTable = new LePagesManager(reader, Offset(LeHeader.e32_objmap), LeHeader.e32_mpages);
        
        var fixupPageOffsets = new FixupPagesManager(reader, Offset(LeHeader.e32_fpagetab), LeHeader.e32_mpages).GetFixupPageOffsets();
        var fixupRecords = new FixupRecordsManager().ReadFixupRecordsTable(reader, Offset(LeHeader.e32_frectab), fixupPageOffsets);
        var imports = new ImportsByFixupsManager().GetImportsByFixups(reader, fixupRecords, Offset(LeHeader.e32_impmod), Offset(LeHeader.e32_impproc));
        
        NonResidentNames = namesTables.NonResidentNames;
        ResidentNames = namesTables.ResidentNames;
        EntryBundles = entryTable.EntryBundles;
        Objects = objectTable.Objects;
        FixupPageOffsets = fixupPageOffsets;
        Pages = pagesTable.Pages;
        
        var offsets = GetObjectsOffsets();
        
        FixupRecords = fixupRecords;
        ImportRecords = imports;
        
        // after all I need Driver information
        if ((LeHeader.e32_mflags & 0x00038000) == 0)
        {
            reader.Close();
            return;
        }

        var driverHeader = new VxdHeader
        {
            e32_major_ddk = LeHeader.Dev386_DDK_Version_1,
            e32_minor_ddk = LeHeader.Dev386_DDK_Version_2,
            e32_devid = LeHeader.Dev386_Device_ID,
            e32_winresoff = LeHeader.e32_winresoff,
            e32_winreslen = LeHeader.e32_winreslen
        };
        var driver = new VxdDriverManager(
            reader,
            driverHeader,
            entryTable.EntryBundles.First(),
            offsets
        );

        VxdHeader = driver.DriverHeader;
        if (VxdHeader.e32_major_ddk == 0)
        {
            // Suppose this sign is a main sign. Public releases of Windows DDK
            // never had 0 major-version value. Those fields are checks by loader.
            reader.Close();
            return;
        }
        
        VxdDescriptionBlock = driver.DriverDescriptionBlock;
        VxdResources = driver.DriverResources;
        
        reader.Close();
    }
    private long[] GetObjectsOffsets()
    {
        var offsets = new List<long>();
        var pages = Pages.Select(x => x.Page).ToArray();
        foreach (var obj in Objects)
        {
            for (uint i = 0; i < obj.PageMapEntries; i++)
            {
                var mapIndex = obj.PageMapIndex + i;
                
                if (mapIndex >= pages.Length)
                {
                    // Out of bounds
                    break;
                }
                
                var pageEntry = pages[(int)mapIndex];
                
                if ((pageEntry.Flags & (byte)Headers.Le.ObjectPage.PageFlags.Legal) != 0)
                {
                    offsets.Add(pageEntry.LongPageIndex * obj.VirtualSegmentSize);
                    break;
                }

                offsets.Add(-1); // If page ZEROED or ITERATED -> skip
            }
        }

        return offsets.ToArray();
    }
}