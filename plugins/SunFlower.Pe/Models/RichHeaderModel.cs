namespace SunFlower.Pe.Models;
///
/// CoffeeLake 2026-*
///
/// Licensed under MIT
///
/// <summary>
/// Decoded Microsoft Rich header. The Rich header sits between the DOS stub
/// and the PE signature and records every compiler/linker tool version used
/// to build the image (the "bill of materials").
/// </summary>
public class RichHeaderModel
{
    /// <summary>
    /// True when a "Rich" marker was found in the DOS stub area.
    /// </summary>
    public bool Present { get; set; }
    /// <summary>
    /// File offset of the "DanS" marker (0 when absent).
    /// </summary>
    public long DansOffset { get; set; }
    /// <summary>
    /// XOR key (doubles as the header checksum).
    /// </summary>
    public uint Key { get; set; }
    /// <summary>
    /// The three 32-bit words after "DanS" are zero in a canonical header.
    /// </summary>
    public bool PaddingValid { get; set; }
    /// <summary>
    /// Recomputed checksum matched the stored key.
    /// </summary>
    public bool ChecksumValid { get; set; }
    /// <summary>
    /// Value recomputed from the DOS header and the records.
    /// </summary>
    public uint ComputedChecksum { get; set; }
    /// <summary>
    /// Decoded product records, in the order they appear.
    /// </summary>
    public List<RichHeaderItem> Items { get; set; } = [];
}
/// <summary>
/// A single decoded Rich header record with resolved annotations.
/// </summary>
public class RichHeaderItem
{
    /// <summary>
    /// Compiler/linker product id (high 16 bits of comp.id).
    /// </summary>
    public ushort ProductId { get; set; }
    /// <summary>
    /// Build number (low 16 bits of comp.id).
    /// </summary>
    public ushort BuildNumber { get; set; }
    /// <summary>
    /// Number of object files produced by this tool version.
    /// </summary>
    public uint Count { get; set; }
    /// <summary>
    /// Tool name (see <see cref="Headers.RichConstants"/>).
    /// </summary>
    public string Name { get; set; } = "Unknown";
    /// <summary>
    /// Toolkit family, (MSVC CL/LNK/VB/MASM).
    /// </summary>
    public string Toolkit { get; set; } = "Unknown";
    /// <summary>
    /// Visual Studio release the product id maps to, if known.
    /// </summary>
    public string VisualStudio { get; set; } = string.Empty;
}