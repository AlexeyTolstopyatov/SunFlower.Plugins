namespace SunFlower.Le.Headers;

public enum EntryBundleType : byte
{
    Unused = 0,
    _16Bit = 1,
    _286CallGate = 2,
    _32Bit = 3,
    Forwarder = 4
}

public abstract class Entry
{
    public abstract EntryBundleType Type { get; }
    public int Ordinal { get; set; }
}

public class Entry16Bit : Entry
{
    public override EntryBundleType Type => EntryBundleType._16Bit;
    public ushort ObjectNumber { get; init; }
    public string EntryName { get; set; } = string.Empty;
    public byte Flags { get; init; }
    public ushort Offset { get; init; }
    public string EntryType => (Flags & 0x01) != 0 ? "[EXPORT]" : "[STATIC]";
}

public class Entry32Bit : Entry
{
    public override EntryBundleType Type => EntryBundleType._32Bit;
    public string EntryName { get; set; } = string.Empty;
    public byte Flags { get; init; }
    public uint Offset { get; init; }
    public int ObjectNumber { get; init; }
    public string EntryType => (Flags & 0x01) != 0 ? "[EXPORT]" : "[STATIC]";
}

public class Entry286CallGate : Entry
{
    public override EntryBundleType Type => EntryBundleType._286CallGate;
    public string EntryName { get; set; } = string.Empty;
    public byte Flags { get; init; }
    public ushort Offset { get; init; }
    public int ObjectNumber { get; init; }
    public ushort CallGateSelector { get; init; } // reserved. Fills by loader
    public string EntryType => (Flags & 0x01) != 0 
        ? "[EXPORT]" 
        : "[STATIC]";
}

public class EntryForwarder : Entry
{
    public override EntryBundleType Type => EntryBundleType.Forwarder;
    public string EntryName { get; set; } = string.Empty;
    public byte Flags { get; init; }
    public ushort Reserved { get; init; }
    public uint ModuleOrdinal { get; init; }
    public uint OffsetOrOrdinal { get; init; }
}

public class EntryUnused : Entry
{
    public override EntryBundleType Type => EntryBundleType.Unused;
    public static string EntryType => "[SPACE]";
}

public class EntryBundle
{
    public byte Count { get; init; }
    public EntryBundleType Type { get; init; }
    public ushort ObjectNumber { get; init; }
    public List<Entry> Entries { get; } = [];

    public string TypeString => Type switch
    {
        EntryBundleType._16Bit => "`.VALID_16`",
        EntryBundleType._32Bit => "`.VALID_32`",
        EntryBundleType._286CallGate => "`.CALLGATE`",
        EntryBundleType.Forwarder => "`.FORWARDER`",
        EntryBundleType.Unused => "`.UNUSED`",
        _ => "`.WHAT?`"
    };
}