using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Services;

public class LxEntryTableManager(BinaryReader reader, uint offset)
{
    public List<EntryBundle> EntryBundles => ReadEntryTable(reader, offset);

    private static List<EntryBundle> ReadEntryTable(BinaryReader reader, uint entryTableOffset)
    {
        reader.BaseStream.Position = entryTableOffset;
        var bundles = new List<EntryBundle>();
        
        while (true)
        {
            var count = reader.ReadByte();
            if (count == 0) break;

            var typeValue = reader.ReadByte();
            var type = (EntryBundleType?)(typeValue & 0x7F) ?? 0;

            ushort objNumber = 0;
            if (type != EntryBundleType.Unused)
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
                        entry = new Entry16Bit
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt16()
                        };
                        break;
                    case EntryBundleType._286CallGate:
                        entry = new Entry286CallGate
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt16(),
                            CallGateSelector = reader.ReadUInt16()
                        };
                        break;
                    case EntryBundleType._32Bit:
                        entry = new Entry32Bit
                        {
                            Flags = reader.ReadByte(),
                            Offset = reader.ReadUInt32()
                        };
                        break;
                    case EntryBundleType.Forwarder:
                        reader.ReadUInt16(); // skip wReserved
                        entry = new EntryForwarder
                        {
                            ModuleOrdinal = reader.ReadUInt16(),
                            OffsetOrOrdinal = reader.ReadUInt32()
                        };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type) + $" = {(byte)type}");
                }

                bundle.Entries.Add(entry);
            }
            bundles.Add(bundle);
        }
        return bundles;
    }
}