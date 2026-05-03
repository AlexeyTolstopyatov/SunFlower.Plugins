using System.Runtime.InteropServices;

namespace Sunflower.Mz;

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MzHeader
{
    public UInt16 e_sign;
    public UInt16 e_cblp;
    public UInt16 e_cp;
    public UInt16 e_relc;
    public UInt16 e_cparhdr;
    public UInt16 e_minep;
    public UInt16 e_maxep;
    public UInt16 ss;
    public UInt16 sp;
    public UInt16 e_crc;
    public UInt16 ip;
    public UInt16 cs;
    public UInt16 e_lfarlc;
    public UInt16 e_ovno;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] 
    public UInt16[] e_res0x1c;
    public UInt16 e_oemid;
    public UInt16 e_oeminfo;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] 
    public UInt16[] e_res_0x28;
    public UInt32 e_lfanew;
}