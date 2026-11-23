using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

/// <summary>
/// IMAGE_BOUND_IMPORT_DESCRIPTOR.
/// Records that this image was linked (bound) against a specific version of a
/// DLL: the loader can then skip rewriting the ImportAddressTable when the on-disk version of
/// that DLL still matches <see cref="TimeDateStamp"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeBoundImportDescriptor
{
    /// <summary> Time/date stamp of the bound DLL (0xFFFFFFFF = not bound). </summary>
    public UInt32 TimeDateStamp;

    /// <summary> Offset of the ASCIIZ module name, relative to the directory start. </summary>
    public UInt16 OffsetModuleName;

    /// <summary> Number of IMAGE_BOUND_FORWARDER_REF entries that follow. </summary>
    public UInt16 NumberOfModuleForwarderRefs;
}