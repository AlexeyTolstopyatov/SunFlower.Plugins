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
        // ReDim main structure by LE program header. I'm going to isolate this data
        // and try to extract next Windows Driver embeddings
        VxdHeader = new VxdHeader
        {
            e32_major_ddk = LeHeader.Dev386_DDK_Version_1,
            e32_minor_ddk = LeHeader.Dev386_DDK_Version_2,
            e32_devid = LeHeader.Dev386_Device_ID,
            e32_winresoff = LeHeader.e32_winresoff,
            e32_winreslen = LeHeader.e32_winreslen
        };
        // Read the first bytes of compiled resource (*.res) offset.
        // It will be needed for me, because I don't know what entry point are represents
        // public driver API. No manuals and articles in the Internet, where I've found it.
        // That's why I've decided to follow this idea:
        reader.BaseStream.Position = LeHeader.e32_winresoff;
        VxdResources = Fill<VxdResources>(reader);
        // Remember of it, because GetPhysicalOffset requires explicit
        // object# for given entry point
        var objectNumber = 0;
        // EntryTable represents complex structure where bundles are not indexed.
        // They will be indexed in the high level (LeTableManager)
        Entry? EntryOrNull()
        {
            // Index of an entry point (1-based). By this index I'm going 
            // to compute <Entry>. Also, I'm sure about that, given Resource->Ordinal field
            // are not refers to the unused entry. (for else, we can't define public API for a driver)
            var ordinal = 0;
            // Iterate all entries here, I want to find Resource->Ordinal and ordinal match.
            foreach (var bundle in EntryBundles)
            {
                objectNumber = bundle.ObjectNumber;
                foreach (var entry in bundle.Entries)
                {
                    ++ordinal;
                    if (ordinal == VxdResources.Ordinal)
                    {
                        return entry;
                    }
                }
            }
            return null;
        }
        // Now we get the Ordinal of "procedure" (hehe, I meant description block metadata)
        // And I'm going to accept challenge - find the address of DDB block by given ordinal
        long ExtractEntryOffset(Entry entry)
        {
            // Despite the fact that entry point might be in various typed bundle
            // I'm expecting once thing. Those virtual/file address. It depends on object number.
            // If object# = 0, address of entrypoint sets to global scope -> will be raw file pointer
            // Otherwise: I need to proceed virtual address by memory pages locations.
            // 
            // Applying ObjectsOffsets here, I'm going to return raw file pointer from the inside  
            var offset = (entry.Type) switch
            {
                EntryBundleType._16Bit => ((Entry16Bit)entry).Offset,
                EntryBundleType._32Bit => ((Entry32Bit)entry).Offset,
                EntryBundleType._286CallGate => ((Entry286CallGate)entry).Offset,
                EntryBundleType.Forwarder => ((EntryForwarder)entry).OffsetOrOrdinal, // fucking unbelievable thing
                _ => throw new Exception($"Given entry type is {entry.Type}")
            };
            
            return objectNumber == 0 ? offset : GetPhysicalOffset(objectNumber, offset);
        }
        // Now I've done. Object number is updated. Entry extracted or null.
        // Last action what we need -> apply it
        var ddbEntry = EntryOrNull();
        if (ddbEntry is null)
            return;
        
        var offset = ExtractEntryOffset(ddbEntry);
        
        reader.BaseStream.Position = offset;
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
    
    public long GetPhysicalOffset(int objectIndex, long rva)
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