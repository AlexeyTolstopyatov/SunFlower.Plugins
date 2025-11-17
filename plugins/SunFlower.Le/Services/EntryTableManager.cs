using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Services;

public class EntryTableManager(
    BinaryReader reader, 
    uint offset, 
    List<ExportRecord> resNames, 
    List<ExportRecord> nonResNames,
    List<string> modules,
    long impProcOffset
    )
{
    public List<EntryBundle> EntryBundles => ReadEntryTable(offset);
    
    private (string dll, string procecure) ReadImportBy(bool isOrdinal, long modIndex, long impOffset)
    {
        var currentPosition = reader.BaseStream.Position;
        byte impLen = 0;
        try
        {
            if (isOrdinal)
                return (modules[(int)(modIndex - 1)], $"@{impOffset}");

            if (modIndex > modules.Count)
                return ("", "?");
            
            reader.BaseStream.Position = impOffset;
            
            impLen = reader.ReadByte();
            var impStr = new string(reader.ReadChars(impLen));
            
            reader.BaseStream.Position = currentPosition;
            return (modules[(int)(modIndex - 1)], impStr);
        }
        catch (Exception e)
        {
            Console.WriteLine($"(ordinal={isOrdinal}; mod[{modIndex}]={impOffset:X}) | 0x{reader.BaseStream.Length:X} | {e}");
            Console.WriteLine($"procedure_len: {impLen}");
        }
        return ("", "!");
    }
    
    private List<EntryBundle> ReadEntryTable(
        uint entryTableOffset)
    {
        reader.BaseStream.Position = entryTableOffset;
        var globalCounter = 1;
        var bundles = new List<EntryBundle>();
        var nonResidentOrdinals = nonResNames.Select(x => x.Ordinal).ToList();
        var residentOrdinals = resNames.Select(x => x.Ordinal).ToList();
        
        string TryGetName(int ordinal)
        {
            if (nonResidentOrdinals.Contains((ushort)ordinal)) 
                return nonResNames.First(x => x.Ordinal == ordinal).String;
            if (residentOrdinals.Contains((ushort)ordinal)) 
                return resNames.First(x => x.Ordinal == ordinal).String;
            
            return "";
        }
        
        while (true)
        {
            var count = reader.ReadByte();
            if (count == 0) break;

            var typeValue = reader.ReadByte();
            var type = (EntryBundleType?)(typeValue & 0x7F) ?? 0;

            ushort objNumber = 0;
            if (type != EntryBundleType.Unused && type != EntryBundleType.Forwarder)
                objNumber = reader.ReadUInt16();
            
            var bundle = new EntryBundle
            {
                Count = count, 
                Type = type, 
                ObjectNumber = objNumber
            };

            for (var i = 0; i < count; i++)
            {
                Entry entry;
                switch (type)
                {
                    case EntryBundleType.Unused:
                        entry = new EntryUnused();
                        
                        break;
                    case EntryBundleType._16Bit:
                        // custom: try to get name by ordinal.
                        
                        entry = new Entry16Bit
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt16(),
                            EntryName = TryGetName(globalCounter)
                        };
                        break;
                    case EntryBundleType._286CallGate:
                        entry = new Entry286CallGate
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt16(),
                            CallGateSelector = reader.ReadUInt16(),
                            EntryName = TryGetName(globalCounter)
                        };
                        break;
                    case EntryBundleType._32Bit:
                        entry = new Entry32Bit
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt32(),
                            EntryName = TryGetName(globalCounter)
                        };
                        break;
                    case EntryBundleType.Forwarder:
                        var fwd = new EntryForwarder
                        {
                            Reserved = reader.ReadUInt16(),
                            Flags = reader.ReadByte(),
                            ModuleOrdinal = reader.ReadUInt16(),
                            OffsetOrOrdinal = reader.ReadUInt32()
                        };
                        // TryGetName but this is a import values
                        var isOrdinal = (fwd.Flags & 0x01) != 0;
                        var impPosition = isOrdinal 
                            ? fwd.OffsetOrOrdinal
                            : impProcOffset + fwd.OffsetOrOrdinal;

                        var nameName = ReadImportBy(isOrdinal, fwd.ModuleOrdinal, impPosition);
                        fwd.EntryName = $"{nameName.dll}::{nameName.procecure}";
                    
                        entry = fwd;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type) + $" = {(byte)type}");
                }

                bundle.Entries.Add(entry);
                ++globalCounter;
            }
            bundles.Add(bundle);
        }
        return bundles;
    }
}