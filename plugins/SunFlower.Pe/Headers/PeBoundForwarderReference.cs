using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

/// <summary>
/// IMAGE_BOUND_FORWARDER_REF.
/// Follows an <see cref="PeBoundImportDescriptor"/> and lists a DLL that the
/// bound DLL forwards to (transitively bound names).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeBoundForwarderReference
{
    /// <summary> Time/date stamp of the forwarded DLL. </summary>
    public UInt32 TimeDateStamp;

    /// <summary> Offset of the ASCIIZ module name, relative to the directory start. </summary>
    public UInt16 OffsetModuleName;

    /// <summary> Reserved, must be zero. </summary>
    public UInt16 Reserved;
}