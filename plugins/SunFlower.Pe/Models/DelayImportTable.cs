// CoffeeLake (C) 2026-*
//
// The DelayImportTable.cs represents the DELAYED IMPORTS data directory (13).
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Models;

/// <summary>
/// Delay-loaded imports. Unlike the static import table these DLLs are not
/// resolved during process start: the image reaches them through a helper
/// routine, so for a disassembler they are an additional set of call targets.
/// </summary>
public class DelayImportTable
{
    public List<DelayImportModule> Modules { get; set; } = [];
}

/// <summary> One delay-loaded DLL and the procedures the image uses from it. </summary>
public class DelayImportModule
{
    public string Name { get; set; } = string.Empty;

    /// <summary> grAttrs flags of the descriptor. </summary>
    public uint Attributes { get; set; }

    /// <summary> Descriptor time/date stamp (0 when not bound). </summary>
    public uint TimeStamp { get; set; }

    /// <summary> Descriptor fields are RVAs (dlattrRva) rather than VAs. </summary>
    public bool UsesRva { get; set; }

    /// <summary> The ImportAddressTable was bound ahead of time (TimeStamp != 0). </summary>
    public bool Bound { get; set; }

    /// <summary> Procedures delayed-loaded from this DLL. </summary>
    public ImportedFunctionCollection Functions { get; init; } = [];
}