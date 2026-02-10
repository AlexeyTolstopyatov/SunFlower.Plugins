namespace SunFlower.Ne.Models;

public class EntryBundle(int ordinalBase, List<Entry> entries)
{
    public int OrdinalBase { get; } = ordinalBase;
    public List<Entry> Entries { get; } = entries;
}

public abstract class Entry { }

public class UnusedEntry : Entry { }

public class FixedEntry(byte segment, byte flags, ushort offset) : Entry
{
    public byte Segment { get; } = segment;
    public byte Flags { get; } = flags;
    public ushort Offset { get; } = offset;
}

public class MoveableEntry(byte flags, byte[] magic, byte segment, ushort offset)
    : Entry
{
    public byte Flags { get; } = flags;
    public byte[] Magic { get; } = magic; // |> INT 0x3F
    public byte Segment { get; } = segment;
    public ushort Offset { get; } = offset;
}