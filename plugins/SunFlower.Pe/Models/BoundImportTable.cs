// CoffeeLake (C) 2026-*
//
// The BoundImportTable.cs represents the BOUND IMPORTS data directory (11).
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Models;

/// <summary>
/// Bound imports: the list of DLLs this image was linked against with a
/// recorded time/date stamp. It carries no addresses, but it tells a reverse
/// engineer which library versions the binary expects and whether the
/// ImportAddressTable rewriting can be skipped at load time.
/// </summary>
public class BoundImportTable
{
    public List<BoundImportModule> Modules { get; set; } = [];
}

/// <summary> One bound DLL and the forwarders it transitively depends on. </summary>
public class BoundImportModule
{
    public string Name { get; set; } = string.Empty;

    /// <summary> Recorded time/date stamp of the bound DLL. </summary>
    public uint TimeDateStamp { get; set; }

    /// <summary> False when the stamp is 0xFFFFFFFF (the image is not bound). </summary>
    public bool Bound { get; set; }

    /// <summary> DLLs the bound module forwards to. </summary>
    public List<BoundForwarderRef> Forwarders { get; init; } = [];
}

/// <summary> A forwarder reference of a bound module. </summary>
public class BoundForwarderRef
{
    public string Name { get; set; } = string.Empty;
    public uint TimeDateStamp { get; set; }
}