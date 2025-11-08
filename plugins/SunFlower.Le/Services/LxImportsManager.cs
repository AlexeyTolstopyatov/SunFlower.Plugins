using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class LxImportsManager
{
    public List<ImportRecord> GetImportsByFixups(
        BinaryReader reader, 
        List<FixupRecord> records, 
        long impModOffset,
        long impProcOffset)
    {
        var modules = GetModules(reader, impModOffset);
        var imports = new List<ImportRecord>();
        foreach (var record in records)
        {
            switch (record.TargetData)
            {
                case FixupTargetImportedOrdinal ordinal:
                    imports.Add(new ImportRecord(
                        modules.ElementAt(ordinal.ModuleOrdinal - 1), 
                        $"@{ordinal.ImportOrdinal}", 
                        reader.BaseStream.Position));
                    break;
                case FixupTargetImportedName name:
                {
                    reader.BaseStream.Position = impProcOffset + name.ProcedureNameOffset;
                    var len = reader.ReadByte();
                    var procName = new String(reader.ReadChars(len));
                
                    imports.Add(new ImportRecord(
                        modules.ElementAt(name.ModuleOrdinal - 1),
                        procName,
                        impProcOffset + name.ProcedureNameOffset
                    ));
                    break;
                }
            }
        }

        return imports;
    } 
    
    private List<string> GetModules(BinaryReader reader, long impModOffset)
    {
        reader.BaseStream.Position = impModOffset;
        var modules = new List<string>();
        var len = reader.ReadByte();
        while (len != 0)
        {
            modules.Add(new string(reader.ReadChars(len)));
            len = reader.ReadByte();
        }

        return modules;
    }
}