namespace SunFlower.Le.Headers;

/// <summary>
/// LE Fixup Address Type (Atp) — первый байт fixup record
/// Bits: 0-3 = address type, 4 = alias, 5 = list mode
/// </summary>
public enum LeFixupAddressType : byte
{
    LowByte = 0,            // 0 - Low byte at the specified offset
    Selector16 = 2,         // 2 - 16-bits selector
    Far32 = 3,              // 3 - 32-bits far pointer (16:32)
    Offset16 = 5,           // 5 - 16-bits Offset
    Far48 = 6,              // 6 - 48-bits far pointer (16:32)
    Offset32 = 7,           // 7 - 32-bits Offset
    OffsetRelEip32 = 8,     // 8 - 32-bits Offset Relative to EIP
}

/// <summary>
/// LE Fixup Relocation Type (Rtp) — второй байт fixup record
/// Bits: 0-1 = relocation type, 2 = additive, 3 = 32-bit target offset,
///       4 = 32-bit additive, 5 = 16-bit object/module ordinal,
///       7 = 8-bit import ordinal
/// </summary>
public enum LeFixupRelocationType : byte
{
    Internal = 0,       // Internal reference
    ImportOrdinal = 1,  // Imported ordinal
    ImportName = 2,     // Imported name
    OsFixup = 3,        // OSFIXUP
}

/// <summary>
/// Source address type extracted from Atp byte
/// </summary>
public readonly struct LeSourceType(byte atp)
{
    public LeFixupAddressType AddressType { get; } = (LeFixupAddressType)(atp & 0x0F);
    public bool IsAlias { get; } = (atp & 0x10) != 0;
    public bool HasSourceList { get; } = (atp & 0x20) != 0;
}

/// <summary>
/// Relocation flags extracted from Rtp byte
/// </summary>
public readonly struct LeRelocationFlags(byte rtp)
{
    public LeFixupRelocationType RelocationType { get; } = (LeFixupRelocationType)(rtp & 0x03);
    public bool HasAdditive { get; } = (rtp & 0x04) != 0;
    public bool Is32BitTargetOffset { get; } = (rtp & 0x08) != 0;
    public bool Is32BitAdditive { get; } = (rtp & 0x10) != 0;
    public bool Is16BitObjectModule { get; } = (rtp & 0x20) != 0;
    public bool Is8BitImportOrdinal { get; } = (rtp & 0x80) != 0;
}

/// <summary>
/// Target data for internal reference fixup
/// </summary>
public readonly struct LeFixupTargetInternal(ushort objectNumber, uint targetOffset)
{
    public ushort ObjectNumber { get; } = objectNumber;
    public uint TargetOffset { get; } = targetOffset;
}

/// <summary>
/// Target data for imported ordinal fixup
/// </summary>
public readonly struct LeFixupTargetImportOrdinal(ushort moduleIndex, uint importOrdinal)
{
    public ushort ModuleIndex { get; } = moduleIndex;
    public uint ImportOrdinal { get; } = importOrdinal;
}

/// <summary>
/// Target data for imported name fixup
/// </summary>
public readonly struct LeFixupTargetImportName(ushort moduleIndex, uint nameOffset)
{
    public ushort ModuleIndex { get; } = moduleIndex;
    public uint NameOffset { get; } = nameOffset;
}

/// <summary>
/// Target data for OS fixup
/// </summary>
public readonly struct LeFixupTargetEntryTable(byte[] data)
{
    public byte[] Data { get; } = data;
}

/// <summary>
/// LE Fixup Record - complete parsed fixup record according to LE specification
/// </summary>
public readonly struct LeFixupRecord(
    LeSourceType sourceType,
    LeRelocationFlags relocationFlags,
    ushort sourceOffset,
    object targetData,
    uint? additiveValue,
    ushort[] sourceOffsetList,
    int logicalPage = -1)
{
    public LeSourceType SourceType { get; } = sourceType;
    public LeRelocationFlags RelocationFlags { get; } = relocationFlags;

    /// <summary>
    /// Source offset within the page, or count of source offsets if list mode
    /// </summary>
    public ushort SourceOffset { get; } = sourceOffset;

    /// <summary>
    /// Target data: LeFixupTargetInternal, LeFixupTargetImportOrdinal,
    /// LeFixupTargetImportName, or LeFixupTargetEntryTable
    /// </summary>
    public object TargetData { get; } = targetData;

    /// <summary>
    /// Additive value (present if HasAdditive is true)
    /// </summary>
    public uint? AdditiveValue { get; } = additiveValue;

    /// <summary>
    /// List of source offsets (present if HasSourceList is true)
    /// </summary>
    public ushort[] SourceOffsetList { get; } = sourceOffsetList;

    public int LogicalPage { get; } = logicalPage;

    public bool IsInternal => RelocationFlags.RelocationType == LeFixupRelocationType.Internal;
    public bool IsImportOrdinal => RelocationFlags.RelocationType == LeFixupRelocationType.ImportOrdinal;
    public bool IsImportName => RelocationFlags.RelocationType == LeFixupRelocationType.ImportName;
    public bool IsOsFixup => RelocationFlags.RelocationType == LeFixupRelocationType.OsFixup;
}