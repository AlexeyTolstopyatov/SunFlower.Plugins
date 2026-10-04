using System.Runtime.InteropServices;

namespace SunFlower.Pe.Headers;

/* ROM Header taken from Ghidra
 *     WORD   Magic; <-- 0x107 expected
 *     BYTE   MajorLinkerVersion;
 *     BYTE   MinorLinkerVersion;
 *     DWORD  SizeOfCode;
 *     DWORD  SizeOfInitializedData;
 *     DWORD  SizeOfUninitializedData;
 *     DWORD  AddressOfEntryPoint;
 *     DWORD  BaseOfCode;
 *     DWORD  BaseOfData;
 *     DWORD  BaseOfBss;
 *     DWORD  GprMask;
 *     DWORD  CprMask[4];
 *     DWORD  GpValue;
 */
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PeOptionalHeaderRom
{
    public UInt16 Magic;
    public Byte MajorLinkerVersion;
    public Byte MinorLinkerVersion;
    public UInt32 SizeOfCode;
    public UInt32 SizeOfInitializedData;
    public UInt32 SizeOfUninitializedData;
    public UInt32 AddressOfEntryPoint;
    public UInt32 BaseOfCode;
    public UInt32 BaseOfData;
    public UInt32 BaseOfBss;
    public UInt32 GprMask;
    [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] 
    public UInt32[] CprMask;
    public UInt32 GpValue;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public struct PeFileHeader
{
    public UInt16 Machine;
    public UInt16 NumberOfSections;
    public UInt32 TimeDateStamp;
    public UInt32 PointerToSymbolTable;
    public UInt32 NumberOfSymbols;
    public UInt16 SizeOfOptionalHeader;
    public UInt16 Characteristics;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public struct PeOptionalHeader
{
    public UInt16 Magic;
    public Byte MajorLinkerVersion;
    public Byte MinorLinkerVersion;
    public UInt32 SizeOfCode;
    public UInt32 SizeOfInitializedData;
    public UInt32 SizeOfUninitializedData;
    public UInt32 AddressOfEntryPoint;
    public UInt32 BaseOfCode;
    public UInt64 ImageBase;
    public UInt32 SectionAlignment;
    public UInt32 FileAlignment;
    public UInt16 MajorOperatingSystemVersion;
    public UInt16 MinorOperatingSystemVersion;
    public UInt16 MajorImageVersion;
    public UInt16 MinorImageVersion;
    public UInt16 MajorSubsystemVersion;
    public UInt16 MinorSubsystemVersion;
    public UInt32 Win32VersionValue;
    public UInt32 SizeOfImage;
    public UInt32 SizeOfHeaders;
    public UInt32 CheckSum;
    public UInt16 Subsystem;
    public UInt16 DllCharacteristics;
    public UInt64 SizeOfStackReserve;
    public UInt64 SizeOfStackCommit;
    public UInt64 SizeOfHeapReserve;
    public UInt64 SizeOfHeapCommit;
    public UInt32 LoaderFlags;
    public UInt32 NumberOfRvaAndSizes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public PeDirectory[] Directories;
}
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public struct PeOptionalHeader32
{
    public UInt16 Magic;
    public Byte MajorLinkerVersion;
    public Byte MinorLinkerVersion;
    public UInt32 SizeOfCode;
    public UInt32 SizeOfInitializedData;
    public UInt32 SizeOfUninitializedData;
    public UInt32 AddressOfEntryPoint;
    public UInt32 BaseOfCode;
    public UInt32 BaseOfData;
    public UInt32 ImageBase;
    public UInt32 SectionAlignment;
    public UInt32 FileAlignment;
    public UInt16 MajorOperatingSystemVersion;
    public UInt16 MinorOperatingSystemVersion;
    public UInt16 MajorImageVersion;
    public UInt16 MinorImageVersion;
    public UInt16 MajorSubsystemVersion;
    public UInt16 MinorSubsystemVersion;
    public UInt32 Win32VersionValue;
    public UInt32 SizeOfImage;
    public UInt32 SizeOfHeaders;
    public UInt32 CheckSum;
    public UInt16 Subsystem;
    public UInt16 DllCharacteristics;
    public UInt32 SizeOfStackReserve;
    public UInt32 SizeOfStackCommit;
    public UInt32 SizeOfHeapReserve;
    public UInt32 SizeOfHeapCommit;
    public UInt32 LoaderFlags;
    public UInt32 NumberOfRvaAndSizes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public PeDirectory[] Directories;

    /// <summary>
    /// Widens a PE32 optional header into the PE32+ layout.
    /// Only the stack/heap size fields differ (32-bit vs 64-bit), so the
    /// remaining fields are copied one-to-one. The Data Directory table is
    /// carried over as-is.
    /// </summary>
    public static PeOptionalHeader Into(ref PeOptionalHeader32 source)
    {
        return new PeOptionalHeader
        {
            Magic = source.Magic,
            MajorLinkerVersion = source.MajorLinkerVersion,
            MinorLinkerVersion = source.MinorLinkerVersion,
            SizeOfCode = source.SizeOfCode,
            SizeOfInitializedData = source.SizeOfInitializedData,
            SizeOfUninitializedData = source.SizeOfUninitializedData,
            AddressOfEntryPoint = source.AddressOfEntryPoint,
            BaseOfCode = source.BaseOfCode,
            // PE32+ has no BaseOfData; ImageBase widens from 32 to 64 bits.
            ImageBase = source.ImageBase,
            SectionAlignment = source.SectionAlignment,
            FileAlignment = source.FileAlignment,
            MajorOperatingSystemVersion = source.MajorOperatingSystemVersion,
            MinorOperatingSystemVersion = source.MinorOperatingSystemVersion,
            MajorImageVersion = source.MajorImageVersion,
            MinorImageVersion = source.MinorImageVersion,
            MajorSubsystemVersion = source.MajorSubsystemVersion,
            MinorSubsystemVersion = source.MinorSubsystemVersion,
            Win32VersionValue = source.Win32VersionValue,
            SizeOfImage = source.SizeOfImage,
            SizeOfHeaders = source.SizeOfHeaders,
            CheckSum = source.CheckSum,
            Subsystem = source.Subsystem,
            DllCharacteristics = source.DllCharacteristics,
            SizeOfStackReserve = source.SizeOfStackReserve,
            SizeOfStackCommit = source.SizeOfStackCommit,
            SizeOfHeapReserve = source.SizeOfHeapReserve,
            SizeOfHeapCommit = source.SizeOfHeapCommit,
            LoaderFlags = source.LoaderFlags,
            NumberOfRvaAndSizes = source.NumberOfRvaAndSizes,
            Directories = source.Directories
        };
    }
}