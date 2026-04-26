using System.Runtime.InteropServices;

namespace SunFlower.Le.Headers.Le;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VxdHeader
{
    public long e32_winresoff;
    public long e32_winreslen;
    public ushort e32_devid;
    public byte e32_minor_ddk;
    public byte e32_major_ddk;
}