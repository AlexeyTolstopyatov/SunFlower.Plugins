using System.Runtime.InteropServices;

namespace SunFlower.Le.Headers.Le;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VxdResources
{
    public byte Type;
    public ushort Id;
    public byte Name;
    public ushort Ordinal;
    public ushort Flags;
    public uint ResourceSize;
    //[MarshalAs(UnmanagedType.Struct)]
    //public VersionInfo VersionInfo;
}