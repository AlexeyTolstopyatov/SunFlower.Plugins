using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

///
/// CoffeeLake 2024-2026
/// This code is JellyBins part for dumping
/// Windows PE32/+ images.
///
/// Licensed under MIT
///
/// <summary>
/// Raw on-disk record of the Microsoft Rich header: a pair of DWORDs.
/// On disk both DWORDs are XOR-encoded with the key stored right after the
/// "Rich" marker; <see cref="Services.RichManager"/> decodes them.
///
///     compId = (ProductId &lt;&lt; 16) | BuildNumber
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RichHeaderRecord
{
    /// <summary> (ProductId &lt;&lt; 16) | BuildNumber. </summary>
    public UInt32 CompId;

    /// <summary> Number of object files produced by that tool version. </summary>
    public UInt32 UsageCount;

    /// <summary> High 16 bits of <see cref="CompId"/>. </summary>
    public UInt16 ProductId => (UInt16)(CompId >> 16);

    /// <summary> Low 16 bits of <see cref="CompId"/>. </summary>
    public UInt16 BuildNumber => (UInt16)(CompId & 0xFFFF);
}