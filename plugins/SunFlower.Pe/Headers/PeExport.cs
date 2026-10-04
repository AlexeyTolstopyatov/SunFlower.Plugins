using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeImageExportDirectory
{
    public UInt32 Characteristics;
    public UInt32 TimeDateStamp;
    public UInt16 MajorVersion;
    public UInt16 MinorVersion;
    public UInt32 Name;
    public UInt32 Base;
    public UInt32 NumberOfFunctions;
    public UInt32 NumberOfNames;
    public UInt32 AddressOfFunctions;
    public UInt32 AddressOfNames;
    public UInt32 AddressOfNameOrdinals;
}