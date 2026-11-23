using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

/// <summary>
/// IMAGE_TLS_DIRECTORY32 (data directory index 9).
/// All addresses are Virtual Addresses (absolute, ImageBase relative), not RVAs.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeTlsDirectory32
{
    public UInt32 StartAddressOfRawData;
    public UInt32 EndAddressOfRawData;
    public UInt32 AddressOfIndex;
    public UInt32 AddressOfCallBacks;
    public UInt32 SizeOfZeroFill;
    public UInt32 Characteristics;
}

/// <summary>
/// IMAGE_TLS_DIRECTORY64 (data directory index 9).
/// The first four fields are pointer-sized; all addresses are Virtual Addresses.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeTlsDirectory64
{
    public UInt64 StartAddressOfRawData;
    public UInt64 EndAddressOfRawData;
    public UInt64 AddressOfIndex;
    public UInt64 AddressOfCallBacks;
    public UInt32 SizeOfZeroFill;
    public UInt32 Characteristics;
}