using System.Text;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class ImportsByFixupsManager
{
    private static string TryRead(ref BinaryReader reader)
    {
        try
        {
            var length = reader.ReadByte();
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
        List<LeFixupRecord> records, 
        string[] impModules,
        long impProcOffset)
    {
        var imports = new List<ImportRecord>();
        foreach (var record in records)
        {
            Console.WriteLine($"rec:{record.TargetData}");
            var failSafeMod = "";
            switch (record.TargetData)
            {
                case LeFixupTargetImportOrdinal ordinal:
                    Console.WriteLine($"#{ordinal.ModuleIndex}::@{ordinal.ImportOrdinal}");
                    failSafeMod = ordinal.ModuleIndex > impModules.Count() || ordinal.ModuleIndex < 1
                        ? $"#{ordinal.ModuleIndex}"
                        : impModules[ordinal.ModuleIndex - 1]; 

                    imports.Add(new ImportRecord(
                        failSafeMod, 
                        $"@{ordinal.ImportOrdinal}", 
                        null));
                    break;
                case LeFixupTargetImportName name:
                {
                    Console.WriteLine($"#{name.ModuleIndex}::+0x{name.NameOffset:X}");
                    reader.BaseStream.Position = impProcOffset + name.NameOffset;
                    failSafeMod = name.ModuleIndex > impModules.Count() || name.ModuleIndex < 1
                        ? $"#{name.ModuleIndex}"
                        : impModules[name.ModuleIndex - 1]; 
                    
                    var impName = TryRead(ref reader);

                    imports.Add(new ImportRecord(
                        failSafeMod,
                        SunFlower.Abstractions.FlowerReport.SafeString(impName),
                        impProcOffset + name.NameOffset
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
            modules.Add(TryRead(ref reader));
            len = reader.ReadByte();
        }

        return modules;
    }
}
