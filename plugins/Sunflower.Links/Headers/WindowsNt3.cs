using System.Runtime.InteropServices;

namespace Sunflower.Links.Headers;
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public struct WindowsNt3
{
    public ushort TimerEmulationFlag;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
    public byte[] Unknown10;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public char[] ConfigNtFileName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public char[] AutoExecNtFileName;
    
    public ushort UnknownToo;
}