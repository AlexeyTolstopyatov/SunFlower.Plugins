namespace SunFlower.Le.Headers;

public struct FixupRecord
{
    public byte Source;
    public byte TargetFlags;
    public ushort SourceOffset;
    public bool HasSourceList;
    public bool HasAdditive;
    public bool Is32BitTarget;
    public bool Is32BitAdditive;
    public bool Is16BitObjectModule;
    public bool Is8BitOrdinal;
    
    // Depends on TargetFlags
    public object TargetData;
    
    // Additive fields
    public uint AdditiveValue;
    public ushort[] SourceOffsetList;
}

public struct FixupPageTableEntry
{
    public uint OffsetToFixupRecords;
}

public struct FixupTargetInternal
{
    public ushort ObjectNumber;  // <--> ushort if Is16BitObjectModule
    public uint TargetOffset;  // <--> ushort if Is32BitTarget
}

public struct FixupTargetImportedOrdinal
{
    public ushort ModuleOrdinal;
    public uint ImportOrdinal;
}

public struct FixupTargetImportedName
{
    public ushort ModuleOrdinal;
    public uint ProcedureNameOffset;
}

public struct FixupTargetEntryTable
{
    public ushort EntryNumber;
}