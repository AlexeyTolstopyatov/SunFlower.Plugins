using System.Runtime.InteropServices;

namespace SunFlower.Le.Headers.Lx;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ObjectPage
{
    public uint PageOffset;
    public ushort DataSize;
    public ushort Flags;
}