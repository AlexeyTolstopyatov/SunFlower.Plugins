using System.Diagnostics;
using System.Text;
using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;

namespace SunFlower.Ne.Services;

/// <summary>
/// Offsets for NE import tables:
/// - ImportedNameTableOffset: offset from NE header start to the Imported Name Table (contains module names)
/// - ModuleRefTableOffset: offset from NE header start to Module Reference Table
/// - ModuleRefCount: number of entries in Module Reference Table
/// </summary>
public struct ImportOffsets(uint importedNameTableOffset, uint moduleRefTableOffset, uint moduleRefCount)
{
    public uint ImportedNameTableOffset { get; } = importedNameTableOffset;
    public uint ModuleRefTableOffset { get; } = moduleRefTableOffset;
    public uint ModuleRefCount { get; } = moduleRefCount;
}

public class NeImportNamesManager(BinaryReader reader, ImportOffsets offsets, List<SegmentModel> segmentModels) : UnsafeManager
{
    public List<ushort> ModuleReferences { get; } = FillModuleReferences(reader, offsets);
    public Dictionary<string, List<Import>> ImportModels { get; } = FillImports(reader, offsets, segmentModels);

    private static Dictionary<string, List<Import>> FillImports(BinaryReader reader, ImportOffsets offsets, List<SegmentModel> segmentModels)
    {
        // Get all import relocations from segments
        var importRelocations = segmentModels
            .SelectMany(segment => segment.Relocations)
            .Where(reloc => reloc.RelocationType.Equals("Import", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var importedModules = new Dictionary<string, List<Import>>();

        foreach (var relocation in importRelocations)
        {
            var moduleName = GetModuleName(reader, offsets, relocation.ModuleIndex);

            if (string.IsNullOrEmpty(moduleName))
                continue;

            var procedure = GetProcedureName(reader, offsets, relocation);
            
            procedure.Module = moduleName;
            procedure.OffsetInSegment = relocation.OffsetInSegment;
            procedure.SegmentNumber = relocation.SegmentNumber;
            procedure.NameOffset = relocation.NameOffset;

            if (!importedModules.ContainsKey(moduleName))
            {
                importedModules[moduleName] = [];
            }

            // Avoid duplicate imports from same module
            if (!importedModules[moduleName].Any(p =>
                    (p.Ordinal != "@0" && p.Ordinal == procedure.Ordinal) ||
                    (!string.IsNullOrEmpty(p.Procedure) && p.Procedure == procedure.Procedure)))
            {
                importedModules[moduleName].Add(procedure);
            }
        }

        // Sort imports within each module
        foreach (var module in importedModules)
        {
            module.Value.Sort((a, b) =>
            {
                if (a.Ordinal != "@0" && b.Ordinal != "@0")
                    return String.Compare(a.Ordinal, b.Ordinal, StringComparison.Ordinal);
                if (a.Ordinal != "@0") return -1;
                if (b.Ordinal != "@0") return 1;
                return string.Compare(a.Procedure, b.Procedure, StringComparison.Ordinal);
            });
        }

        return importedModules;
    }

    /// <summary>
    /// Gets module name by module index from Module Reference Table
    /// Module index is 1-based (index 0 = unused)
    /// </summary>
    private static string GetModuleName(BinaryReader reader, ImportOffsets offsets, ushort moduleIndex)
    {
        try
        {
            if (moduleIndex == 0) return string.Empty;

            // Module Reference Table contains offsets into Imported Name Table
            var originalPos = reader.BaseStream.Position;
            reader.BaseStream.Position = offsets.ModuleRefTableOffset + 2 * (moduleIndex - 1);
            var nameOffsetInImportTable = reader.ReadUInt16();

            // Now read the module name from Imported Name Table
            reader.BaseStream.Position = offsets.ImportedNameTableOffset + nameOffsetInImportTable;
            var result = ReadPascalString(reader);
            reader.BaseStream.Position = originalPos;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading module name for index {moduleIndex}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets procedure name from Imported Name Table using relocation info
    /// </summary>
    private static Import GetProcedureName(BinaryReader reader, ImportOffsets offsets, Relocation relocation)
    {
        var procedure = new Import
        {
            ModuleIndex = relocation.ModuleIndex,
            OffsetInSegment = relocation.OffsetInSegment
        };

        if (relocation.Ordinal > 0)
        {
            // Import by ordinal - no name available in table
            procedure.Ordinal = "@" + relocation.Ordinal;
            procedure.Procedure = "@" + relocation.Ordinal;
        }
        else if (relocation.NameOffset > 0)
        {
            // Import by name - read procedure name from Imported Name Table
            try
            {
                var originalPos = reader.BaseStream.Position;
                reader.BaseStream.Position = offsets.ImportedNameTableOffset + relocation.NameOffset;
                procedure.Procedure = ReadPascalString(reader);
                procedure.Ordinal = "@0";
                reader.BaseStream.Position = originalPos;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading procedure name at offset {relocation.NameOffset}: {ex.Message}");
                procedure.Procedure = $"?0x{relocation.NameOffset:X8}";
            }
        }
        else
        {
            procedure.Procedure = "Anonymous";
        }

        return procedure;
    }

    private static string ReadPascalString(BinaryReader reader)
    {
        var pstrLength = reader.ReadByte();
        return pstrLength == 0 ? "" : Encoding.ASCII.GetString(reader.ReadBytes(pstrLength));
    }

    private static List<ushort> FillModuleReferences(BinaryReader reader, ImportOffsets offsets)
    {
        var modules = new List<ushort>();
        reader.BaseStream.Position = offsets.ModuleRefTableOffset;

        for (var i = 0; i < offsets.ModuleRefCount; i++)
        {
            var mod = reader.ReadUInt16();
            modules.Add(mod);
        }

        return modules;
    }
}