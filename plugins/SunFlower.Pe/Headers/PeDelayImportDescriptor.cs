using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

/// <summary>
/// IMAGE_DELAY_IMPORT_DESCRIPTOR (ImgDelayDescr).
/// Describes one delay-loaded DLL: the image calls <c>__delayLoadHelper2</c>
/// on the first access and the loader resolves the real import then.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeDelayImportDescriptor
{
    /// <summary> grAttrs. When bit 0 (dlattrRva) is set all fields are RVAs. </summary>
    public UInt32 Attributes;

    /// <summary> RVA to the imported DLL name. </summary>
    public UInt32 Name;

    /// <summary> RVA to a slot that receives the module handle. </summary>
    public UInt32 ModuleHandle;

    /// <summary> RVA to the Import Address Table (filled in at load time). </summary>
    public UInt32 Iat;

    /// <summary> RVA to the Import Name Table (unchanged, holds names/ordinals). </summary>
    public UInt32 Int;

    /// <summary> RVA to the optional bound ImportAddressTable. </summary>
    public UInt32 BoundIat;

    /// <summary> RVA to the optional unload ImportAddressTable (a copy of the original INT). </summary>
    public UInt32 UnloadIat;

    /// <summary> Time/date stamp; non-zero means the ImportAddressTable is already bound. </summary>
    public UInt32 TimeStamp;
}