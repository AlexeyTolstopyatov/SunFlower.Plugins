using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Lx;
using SunFlower.Le.Models.Le;
using EntryBundle = SunFlower.Le.Headers.Lx.EntryBundle;

namespace SunFlower.Le.Services;

public class LxDumpManager : UnsafeManager
{
    public MzHeader MzHeader { get; }
    public LxHeader LxHeader { get; }
    public List<ExportRecord> ResidentNames { get; }
    public List<ExportRecord> NonResidentNames { get; }
    public List<string> ImportingModules { get; set; }
    public List<EntryBundle> EntryBundles { get; }
    public List<Headers.Lx.Object> Objects { get; }
    public List<ObjectPageModel> Pages { get; set; }
    public List<FixupRecord> FixupRecords { get; }
    public List<FixupPageRecord> FixupPageOffsets { get; }
    public List<ImportRecord> ImportRecords { get; }
    private uint _offset;

    public uint Offset(uint addr) => _offset + addr;
    
    public LxDumpManager(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        MzHeader = Fill<MzHeader>(reader);
        if (MzHeader.e_sign is not 0x5a4d and 0x4d5a)
            throw new NotSupportedException("Doesn't have MZ magic");

        _offset = MzHeader.e_lfanew;
        stream.Position = MzHeader.e_lfanew;
        LxHeader = Fill<LxHeader>(reader);
        if (LxHeader.e32_magic != 0x454c && LxHeader.e32_magic != 0x4c45)
            if (LxHeader.e32_magic != 0x584c && LxHeader.e32_magic != 0x4c58)
                throw new NotSupportedException("Doesn't have 'LX' magic");
        
        var namesTables = new NamesTablesManager(reader, Offset(LxHeader.e32_restab), LxHeader.e32_nrestab);
        var importNames = new ImportNamesManager(reader, Offset(LxHeader.e32_impmod));
        var entryTable = new EntryTableManager(reader, Offset(LxHeader.e32_enttab), namesTables.ResidentNames, namesTables.NonResidentNames, importNames.ImportingModules, Offset(LxHeader.e32_impproc));
        var objectTable = new LxObjectsManager(reader, Offset(LxHeader.e32_objtab), LxHeader.e32_objcnt);
        var pagesTable = new LePagesManager(reader, Offset(LxHeader.e32_objmap), LxHeader.e32_pagesum);
        var fixupPageOffsets = new FixupPagesManager(reader, Offset(LxHeader.e32_fpagetab), LxHeader.e32_mpages, LxHeader.e32_pageshift).GetFixupPageOffsets();
        var fixupRecords = new FixupRecordsManager().ReadFixupRecordsTable(reader, LxHeader, MzHeader.e_lfanew, fixupPageOffsets);
        var imports = new ImportsByFixupsManager().GetImportsByFixups(reader, fixupRecords, Offset(LxHeader.e32_impmod), Offset(LxHeader.e32_impproc));
        
        NonResidentNames = namesTables.NonResidentNames;
        ResidentNames = namesTables.ResidentNames;
        ImportingModules = importNames.ImportingModules;
        EntryBundles = entryTable.EntryBundles;
        Objects = objectTable.Objects;
        FixupPageOffsets = fixupPageOffsets;
        Pages = pagesTable.Pages;
        FixupRecords = fixupRecords;
        ImportRecords = imports;
        
        reader.Close();
    }
}