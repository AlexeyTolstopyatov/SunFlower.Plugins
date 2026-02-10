namespace SunFlower.Le.Headers.Le;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LeHeader
{
    public const ushort LeCpu286 = 1;
    public const ushort LeCpu386 = 2;
    public const ushort LeCpu486 = 3;
    public const ushort LeCpu586 = 4;
    public const ushort LeCpuI860 = 0x20;
    public const ushort LeCpuN11 = 0x21;
    public const ushort LeCpuR2000 = 0x40;
    public const ushort LeCpuR6000 = 0x41;
    public const ushort LeCpuR4000 = 0x42;
    public const uint LeTypeInitPer = 1 << 2;     // initialise per process.
    public const uint LeTypeIntFixup = 1 << 4;     // no internal fixups.
    public const uint LeTypeExtFixup = 1 << 5;     // no external fixups.
    public const uint LeTypeNoLoad = 1 << 13;    // module not loadable.
    public const uint LeTypeDll = 1 << 15;    // DLL

    
    public const ushort LeOsOs2 = 1;
    public const ushort LeOsWindows = 2;
    public const ushort LeOsDos4 = 3;
    public const ushort LeOsWin386 = 4;
    public ushort e32_magic;
    public byte e32_border;
    public byte e32_worder;
    public uint e32_level;
    public ushort e32_cpu;
    public ushort e32_os;
    public uint e32_ver;
    public uint e32_mflags;
    public uint e32_mpages;
    public uint e32_startobj;
    public uint e32_eip;
    public uint e32_stackobj;
    public uint e32_esp;
    public uint e32_pagesize;
    public uint e32_lastpagesize;
    public uint e32_fixupsize;
    public uint e32_fixupsum;
    public uint e32_ldrsize;
    public uint e32_ldrsum;
    public uint e32_objtab;
    public uint e32_objcnt;
    public uint e32_objmap;
    public uint e32_itermap;
    public uint e32_rsrctab;
    public uint e32_rsrccnt;
    public uint e32_restab;
    public uint e32_enttab;
    public uint e32_dirtab;
    public uint e32_dircnt;
    public uint e32_fpagetab;
    public uint e32_frectab;
    public uint e32_impmod;
    public uint e32_impmodcnt;
    public uint e32_impproc;
    public uint e32_pagesum;
    public uint e32_datapage;
    public uint e32_preload;
    public uint e32_nrestab;
    public uint e32_cbnrestab;
    public uint e32_nressum;
    public uint e32_autodata;
    public uint e32_debuginfo;
    public uint e32_debuglen;
    public uint e32_instpreload;
    public uint e32_instdemand;
    public uint e32_heapsize;
    public uint e32_stacksize;

    /// Padding between two Windows386 headers:
    /// After LE header can follow the VXD_HEADER which
    /// describes library as specific Windows Virtual Driver 
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public uint[] e32_padding;
    
    public uint e32_winresoff ;
    public uint e32_winreslen ;
    public ushort Dev386_Device_ID;
    public byte Dev386_DDK_Version_1;
    public byte Dev386_DDK_Version_2;
}

