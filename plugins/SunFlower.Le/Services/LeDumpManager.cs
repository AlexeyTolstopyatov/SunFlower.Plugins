using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;
using EntryBundle = SunFlower.Le.Headers.EntryBundle;
using Object = SunFlower.Le.Headers.Le.Object;
using ObjectPage = SunFlower.Le.Models.Le.ObjectPage;

namespace SunFlower.Le.Services;

public class LeDumpManager : UnsafeManager
{
    public MzHeader MzHeader { get; }
    public LeHeader LeHeader { get; }
    public List<ExportRecord> ResidentNames { get; }
    public List<ExportRecord> NonResidentNames { get; }
    public List<EntryBundle> EntryBundles { get; }
    public List<Object> Objects { get; }
    public List<ObjectPage> Pages { get; }
    public List<FixupRecord> FixupRecords { get; }
    public List<FixupPageRecord> FixupPageOffsets { get; }
    public List<ImportRecord> ImportRecords { get; }
    public ObjectPageOffsetPair[] ObjectsOffsets;
    public VxdHeader VxdHeader { get; set; }
    public VxdDescriptionBlock VxdDescriptionBlock { get; set; }
    public VxdResources VxdResources { get; set; }
    public bool IsDriver { get; private set; }
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
        var importNames = new ImportNamesManager(reader, Offset(LeHeader.e32_impmod), LeHeader.e32_impmodcnt);
        var entryTable = new EntryTableManager(reader, Offset(LeHeader.e32_enttab), namesTables.ResidentNames, namesTables.NonResidentNames, importNames.ImportingModules, Offset(LeHeader.e32_impproc));
        var objectTable = new LeObjectsManager(reader, Offset(LeHeader.e32_objtab), LeHeader.e32_objcnt);
        var pagesTable = new LePagesManager(reader, Offset(LeHeader.e32_objmap), LeHeader.e32_mpages);
        
        var fixupPageOffsets = new FixupPagesManager(reader, Offset(LeHeader.e32_fpagetab), LeHeader.e32_mpages).GetFixupPageOffsets();
        var fixupRecords = new FixupRecordsManager().ReadFixupRecordsTable(reader, Offset(LeHeader.e32_frectab), fixupPageOffsets);
        var imports = new ImportsByFixupsManager().GetImportsByFixups(reader, fixupRecords, importNames.ImportingModules.ToArray(), Offset(LeHeader.e32_impproc));
        
        NonResidentNames = namesTables.NonResidentNames;
        ResidentNames = namesTables.ResidentNames;
        EntryBundles = entryTable.EntryBundles;
        Objects = objectTable.Objects;
        FixupPageOffsets = fixupPageOffsets;
        Pages = pagesTable.Pages;
        
        FixupRecords = fixupRecords;
        ImportRecords = imports;
        
        ObjectsOffsets = GetObjectsOffsets(); // <-- get the tip
        
        // after all I need Driver information
        if (LeHeader.e32_winresoff == 0)
        {
            IsDriver = false;
            reader.Close();
            return;
        }

        IsDriver = true;
        CollectVirtualDriverMetadata(reader);
        
        reader.Close();
    }

    private void CollectVirtualDriverMetadata(BinaryReader reader)
    {
        VxdHeader = new VxdHeader
        {
            e32_major_ddk = LeHeader.Dev386_DDK_Version_1,
            e32_minor_ddk = LeHeader.Dev386_DDK_Version_2,
            e32_devid = LeHeader.Dev386_Device_ID,
            e32_winresoff = LeHeader.e32_winresoff,
            e32_winreslen = LeHeader.e32_winreslen
        };
        
        reader.BaseStream.Position = LeHeader.e32_winresoff;
        VxdResources = Fill<VxdResources>(reader);
        // Now we get the Ordinal of "procedure" (hehe, i meant description block metadata)
        // And i'm going to accept challenge - find the address of DDB block by given ordinal
        long ExtractEntryOffset(Entry entry)
        {
            switch (entry.Type)
            {
                case EntryBundleType._16Bit:
                    var e16 = (Entry16Bit)entry;
                    return e16.Offset;
                case  EntryBundleType._32Bit:
                    var e32 = (Entry32Bit)entry;
                    return e32.Offset;
                case EntryBundleType._286CallGate:
                    var gate = (Entry286CallGate)entry;
                    return gate.Offset;
            }

            return -1;
        }
        // var ordinal = 1;
        // foreach (var entry in EntryBundles.Where(bundle => bundle.Type != EntryBundleType.Unused).SelectMany(bundle => bundle.Entries))
        // {
        //     if (ordinal == VxdResources.Ordinal)
        //     {
        //         ddbEntry = entry;
        //         return;
        //     }
        //     ++ordinal;
        // }
        var offset = ExtractEntryOffset(EntryBundles[0].Entries[0]);
        var ddbOffset = GetPhysicalOffset(EntryBundles[0].ObjectNumber, offset);
        if (ddbOffset is null or -1)
            return;

        reader.BaseStream.Position = ddbOffset.Value;
        VxdDescriptionBlock = Fill<VxdDescriptionBlock>(reader);
    }
    
    private ObjectPageOffsetPair[] GetObjectsOffsets()
    {
        // I've decided after hundred years of suffering by various archived docs
        // page_offset := e32_datapage + (page# - 1) * e32_pagesize
        var pages = Pages.Select(x => x.Page).ToArray();
        var firstPageOffset = LeHeader.e32_datapage;
        var pageSize = LeHeader.e32_pagesize;

        return pages.Select(page => new ObjectPageOffsetPair(page.LongPageIndex, firstPageOffset + (page.LongPageIndex - 1) * pageSize)).ToArray();
    }
    
    public long? GetPhysicalOffset(int objectIndex, long rva)
    {
        if (objectIndex < 1 || objectIndex > Objects.Count)
        {
            Console.WriteLine($"object#{objectIndex}. Out of bounds ({Objects.Count})");
            return -1;
        }

        var obj = Objects[objectIndex - 1];
        var pageOffset = ObjectsOffsets[obj.PageMapIndex - 1].Offset;
        
        return pageOffset + rva;
    }
}