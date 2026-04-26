using System.Text;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class ImportsByFixupsManager
{
    private string TryRead(ref BinaryReader reader, int length)
    {
        try
        {
            return length != 0 
                ? Encoding.ASCII.GetString(reader.ReadBytes(length)) 
                : string.Empty;
        } 
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return "?";
        }
    }

    public List<ImportRecord> GetImportsByFixups(
        BinaryReader reader, 
        List<FixupRecord> records, 
        string[] impModules,
        long impProcOffset)
    {
        var imports = new List<ImportRecord>();
        foreach (var record in records)
        {
            switch (record.TargetData)
            {
                case FixupTargetImportedOrdinal ordinal:
                    imports.Add(new ImportRecord(
                        impModules[ordinal.ModuleOrdinal - 1], 
                        $"@{ordinal.ImportOrdinal}", 
                        null));
                    break;
                case FixupTargetImportedName name:
                {
                    reader.BaseStream.Position = impProcOffset + name.ProcedureNameOffset;
                    var len = reader.ReadByte();

                    imports.Add(new ImportRecord(
                        impModules[name.ModuleOrdinal - 1],
                        TryRead(ref reader, len),
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
            modules.Add(TryRead(ref reader, len));
            len = reader.ReadByte();
        }

        return modules;
    }
}