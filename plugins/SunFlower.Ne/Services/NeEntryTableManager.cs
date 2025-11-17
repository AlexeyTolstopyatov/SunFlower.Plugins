using SunFlower.Ne.Headers;
using SunFlower.Ne.Models;

namespace SunFlower.Ne.Services;

public class NeEntryTableManager(BinaryReader reader, uint offset, uint bundlesCount, List<Name> nonResidentNames, List<Name> residentNames)
{
    public List<NeEntryBundle> EntryBundles => FindEntryBundles();

    private List<NeEntryBundle> FindEntryBundles()
    {
        reader.BaseStream.Position = offset;
        var nonResidentOrdinals = nonResidentNames.Select(x => x.Ordinal).ToList();
        var residentOrdinals = residentNames.Select(x => x.Ordinal).ToList();
        
        string TryGetName(int index)
        {
            if (nonResidentOrdinals.Contains((ushort)index)) 
                return nonResidentNames.First(x => x.Ordinal == index).String;
            if (residentOrdinals.Contains((ushort)index))
                return residentNames.First(x => x.Ordinal == index).String;

            return "";
        }

        var bundles = new List<NeEntryBundle>();
        var bytesRemaining = (int)bundlesCount;
        var currentOrdinal = 1;
        
        while (bytesRemaining > 0)
        {
            var count = reader.ReadByte();
            bytesRemaining--;

            if (count == 0) // <-- end
                break;

            var segId = reader.ReadByte();
            bytesRemaining--;

            var entries = new List<EntryTableModel>();

            if (segId == 0) // !UNuSED
            {
                for (var i = 0; i < count; i++)
                {
                    entries.Add(new EntryTableModel(true, false, 0)
                    {
                        Ordinal = (ushort)currentOrdinal++,
                        Segment = segId,
                    });
                }
            }
            else // CONST or MOVEABLE
            {
                var isMoveable = segId == 0xFF;
                var entrySize = isMoveable ? 6 : 3;
                var bundleDataSize = count * entrySize;

                if (bundleDataSize > bytesRemaining)
                    throw new InvalidDataException(
                        $"Inexact length: expected {bundleDataSize} bytes, got {bytesRemaining}");

                for (var i = 0; i < count; i++)
                {
                    if (isMoveable)
                    {
                        var flags = reader.ReadByte();
                        var magic = reader.ReadBytes(2);
                        var segment = reader.ReadByte();
                        var entOffset = reader.ReadUInt16();

                        entries.Add(new EntryTableModel(
                            false, true ,flags)
                        {
                            Offset = entOffset,
                            Segment = segment,
                            Ordinal = (ushort)currentOrdinal,
                            Name = TryGetName(currentOrdinal)
                        });
                    }
                    else
                    {
                        var flags = reader.ReadByte();
                        var entOffset = reader.ReadUInt16();

                        entries.Add(new EntryTableModel(
                            false, false, flags)
                        {
                            Offset = entOffset,
                            Segment = segId,
                            Ordinal = (ushort)currentOrdinal,
                            Name = TryGetName(currentOrdinal)
                        });
                    }

                    currentOrdinal++;
                }

                bytesRemaining -= bundleDataSize;
            }

            bundles.Add(new NeEntryBundle()
            {
                EntryPoints = entries
            });
        }

        return bundles;
    }
}