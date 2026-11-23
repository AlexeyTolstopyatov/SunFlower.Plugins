using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Models;

/// <summary>
/// Thunk table for each importing module. Unlike Original first thunk the Address Table
/// stores slots which loader replaces with valid virtual addresses
/// Import Address Table (ImportAddressTable): Thunk table (<see cref="PeImportDescriptor.FirstThunk"/>)
/// </summary>
public class ImportAddressTable
{
    public List<AddressTableModule> Modules { get; set; } = [];
}

public class AddressTableModule
{
    public string Name { get; set; } = string.Empty;
    public List<AddressTableFunction> Functions { get; set; } = [];
    public uint FirstThunkRva { get; set; }
}

public class AddressTableFunction
{
    public string Name { get; set; } = string.Empty;
    public uint Ordinal { get; set; }
    public ushort Hint { get; set; }
    /// <summary>
    /// Address where loader will place a procedure pointer
    /// </summary>
    public ulong SlotRva { get; set; }
    public ulong SlotFileOffset { get; set; }
    public ulong ThunkValue { get; set; }
    public bool IsOrdinal { get; set; }
}