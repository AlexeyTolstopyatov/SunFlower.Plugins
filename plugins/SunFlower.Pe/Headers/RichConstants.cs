// CoffeeLake (C) 2026-*
//
// RichConstants.cs - Microsoft Rich header constants.
//
// This file is intentionally self-contained and has no dependencies on the
// rest of the plugin: it can be copied verbatim to any other project/library
// that needs to identify Rich header tool versions (MSVC, VB6, MASM, ...).
//
// Sources for the product id table:
//     https://github.com/dishather/richprint            (comp_id.txt)
//     https://gist.github.com/skochinsky/07c8e95e33d9429d81a75622b5d24c8b
//     https://github.com/saferwall/pe                   (richheader.go)
//     https://github.com/hasherezade/bearparser         (RichHdrWrapper.cpp)
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Headers;

/// <summary>
/// Constants and lookup helpers for the Microsoft Rich header.
/// Kept dependency-free so the file can travel with the library.
/// </summary>
public static class RichConstants
{
    /// <summary> 'DanS' marker as a little-endian DWORD. </summary>
    public const uint DansSignature = 0x536E6144;

    /// <summary> 'Rich' marker as a little-endian DWORD. </summary>
    public const uint RichSignature = 0x68636952;

    /// <summary> Number of zero DWORDs that follow the "DanS" marker. </summary>
    public const int PaddingCount = 3;

    /// <summary> Start of the DOS stub (right after the fixed DOS header). </summary>
    public const int DosStubOffset = 0x40;

    // Product id -&gt; symbolic tool name.
    //
    // NOTE: the ids 0x99 and 0x9F..0xA9 are not covered by the public
    // references listed above and are deliberately left out; a lookup for them
    // returns "Unknown" while the raw product id / build number are still
    // reported by the parser. Adding them later only means adding one line.
    private static readonly Dictionary<ushort, string> Names = new()
    {
        { 0x0000, "" },
        { 0x0001, "Import0" },
        { 0x0002, "Linker510" },
        { 0x0003, "Cvtomf510" },
        { 0x0004, "Linker600" },
        { 0x0005, "Cvtomf600" },
        { 0x0006, "Cvtres500" },
        { 0x0007, "Utc11_Basic" },
        { 0x0008, "Utc11_C" },
        { 0x0009, "Utc12_Basic" },
        { 0x000A, "Utc12_C" },
        { 0x000B, "Utc12_CPP" },
        { 0x000C, "AliasObj60" },
        { 0x000D, "VisualBasic60" },
        { 0x000E, "Masm613" },
        { 0x000F, "Masm710" },
        { 0x0010, "Linker511" },
        { 0x0011, "Cvtomf511" },
        { 0x0012, "Masm614" },
        { 0x0013, "Linker512" },
        { 0x0014, "Cvtomf512" },
        { 0x0015, "Utc12_C_Std" },
        { 0x0016, "Utc12_CPP_Std" },
        { 0x0017, "Utc12_C_Book" },
        { 0x0018, "Utc12_CPP_Book" },
        { 0x0019, "Implib700" },
        { 0x001A, "Cvtomf700" },
        { 0x001B, "Utc13_Basic" },
        { 0x001C, "Utc13_C" },
        { 0x001D, "Utc13_CPP" },
        { 0x001E, "Linker610" },
        { 0x001F, "Cvtomf610" },
        { 0x0020, "Linker601" },
        { 0x0021, "Cvtomf601" },
        { 0x0022, "Utc12_1_Basic" },
        { 0x0023, "Utc12_1_C" },
        { 0x0024, "Utc12_1_CPP" },
        { 0x0025, "Linker620" },
        { 0x0026, "Cvtomf620" },
        { 0x0027, "AliasObj70" },
        { 0x0028, "Linker621" },
        { 0x0029, "Cvtomf621" },
        { 0x002A, "Masm615" },
        { 0x002B, "Utc13_LTCG_C" },
        { 0x002C, "Utc13_LTCG_CPP" },
        { 0x002D, "Masm620" },
        { 0x002E, "ILAsm100" },
        { 0x002F, "Utc12_2_Basic" },
        { 0x0030, "Utc12_2_C" },
        { 0x0031, "Utc12_2_CPP" },
        { 0x0032, "Utc12_2_C_Std" },
        { 0x0033, "Utc12_2_CPP_Std" },
        { 0x0034, "Utc12_2_C_Book" },
        { 0x0035, "Utc12_2_CPP_Book" },
        { 0x0036, "Implib622" },
        { 0x0037, "Cvtomf622" },
        { 0x0038, "Cvtres501" },
        { 0x0039, "Utc13_C_Std" },
        { 0x003A, "Utc13_CPP_Std" },
        { 0x003B, "Cvtpgd1300" },
        { 0x003C, "Linker622" },
        { 0x003D, "Linker700" },
        { 0x003E, "Export622" },
        { 0x003F, "Export700" },
        { 0x0040, "Masm700" },
        { 0x0041, "Utc13_POGO_I_C" },
        { 0x0042, "Utc13_POGO_I_CPP" },
        { 0x0043, "Utc13_POGO_O_C" },
        { 0x0044, "Utc13_POGO_O_CPP" },
        { 0x0045, "Cvtres700" },
        { 0x0046, "Cvtres710p" },
        { 0x0047, "Linker710p" },
        { 0x0048, "Cvtomf710p" },
        { 0x0049, "Export710p" },
        { 0x004A, "Implib710p" },
        { 0x004B, "Masm710p" },
        { 0x004C, "Utc1310p_C" },
        { 0x004D, "Utc1310p_CPP" },
        { 0x004E, "Utc1310p_C_Std" },
        { 0x004F, "Utc1310p_CPP_Std" },
        { 0x0050, "Utc1310p_LTCG_C" },
        { 0x0051, "Utc1310p_LTCG_CPP" },
        { 0x0052, "Utc1310p_POGO_I_C" },
        { 0x0053, "Utc1310p_POGO_I_CPP" },
        { 0x0054, "Utc1310p_POGO_O_C" },
        { 0x0055, "Utc1310p_POGO_O_CPP" },
        { 0x0056, "Linker624" },
        { 0x0057, "Cvtomf624" },
        { 0x0058, "Export624" },
        { 0x0059, "Implib624" },
        { 0x005A, "Linker710" },
        { 0x005B, "Cvtomf710" },
        { 0x005C, "Export710" },
        { 0x005D, "Implib710" },
        { 0x005E, "Cvtres710" },
        { 0x005F, "Utc1310_C" },
        { 0x0060, "Utc1310_CPP" },
        { 0x0061, "Utc1310_C_Std" },
        { 0x0062, "Utc1310_CPP_Std" },
        { 0x0063, "Utc1310_LTCG_C" },
        { 0x0064, "Utc1310_LTCG_CPP" },
        { 0x0065, "Utc1310_POGO_I_C" },
        { 0x0066, "Utc1310_POGO_I_CPP" },
        { 0x0067, "Utc1310_POGO_O_C" },
        { 0x0068, "Utc1310_POGO_O_CPP" },
        { 0x0069, "AliasObj710" },
        { 0x006A, "AliasObj710p" },
        { 0x006B, "Cvtpgd1310" },
        { 0x006C, "Cvtpgd1310p" },
        { 0x006D, "Utc1400_C" },
        { 0x006E, "Utc1400_CPP" },
        { 0x006F, "Utc1400_C_Std" },
        { 0x0070, "Utc1400_CPP_Std" },
        { 0x0071, "Utc1400_LTCG_C" },
        { 0x0072, "Utc1400_LTCG_CPP" },
        { 0x0073, "Utc1400_POGO_I_C" },
        { 0x0074, "Utc1400_POGO_I_CPP" },
        { 0x0075, "Utc1400_POGO_O_C" },
        { 0x0076, "Utc1400_POGO_O_CPP" },
        { 0x0077, "Cvtpgd1400" },
        { 0x0078, "Linker800" },
        { 0x0079, "Cvtomf800" },
        { 0x007A, "Export800" },
        { 0x007B, "Implib800" },
        { 0x007C, "Cvtres800" },
        { 0x007D, "Masm800" },
        { 0x007E, "AliasObj800" },
        { 0x007F, "PhoenixPrerelease" },
        { 0x0080, "Utc1400_CVTCIL_C" },
        { 0x0081, "Utc1400_CVTCIL_CPP" },
        { 0x0082, "Utc1400_LTCG_MSIL" },
        { 0x0083, "Utc1500_C" },
        { 0x0084, "Utc1500_CPP" },
        { 0x0085, "Utc1500_C_Std" },
        { 0x0086, "Utc1500_CPP_Std" },
        { 0x0087, "Utc1500_CVTCIL_C" },
        { 0x0088, "Utc1500_CVTCIL_CPP" },
        { 0x0089, "Utc1500_LTCG_C" },
        { 0x008A, "Utc1500_LTCG_CPP" },
        { 0x008B, "Utc1500_LTCG_MSIL" },
        { 0x008C, "Utc1500_POGO_I_C" },
        { 0x008D, "Utc1500_POGO_I_CPP" },
        { 0x008E, "Utc1500_POGO_O_C" },
        { 0x008F, "Utc1500_POGO_O_CPP" },
        { 0x0090, "Cvtpgd1500" },
        { 0x0091, "Linker900" },
        { 0x0092, "Export900" },
        { 0x0093, "Implib900" },
        { 0x0094, "Cvtres900" },
        { 0x0095, "Masm900" },
        { 0x0096, "AliasObj900" },
        { 0x0097, "Resource900" },
        { 0x0098, "AliasObj1000" },
        // 0x0099 - not attested by public references
        { 0x009A, "Cvtres1000" },
        { 0x009B, "Export1000" },
        { 0x009C, "Implib1000" },
        { 0x009D, "Linker1000" },
        { 0x009E, "Masm1000" },
        // 0x009F..0x00A9 - not attested by public references
        { 0x00AA, "Utc1600_C" },
        { 0x00AB, "Utc1600_CPP" },
        { 0x00AC, "Utc1600_CVTCIL_C" },
        { 0x00AD, "Utc1600_CVTCIL_CPP" },
        { 0x00AE, "Utc1600_LTCG_C" },
        { 0x00AF, "Utc1600_LTCG_CPP" },
        { 0x00B0, "Utc1600_LTCG_MSIL" },
        { 0x00B1, "Utc1600_POGO_I_C" },
        { 0x00B2, "Utc1600_POGO_I_CPP" },
        { 0x00B3, "Utc1600_POGO_O_C" },
        { 0x00B4, "Utc1600_POGO_O_CPP" },
        { 0x00B5, "AliasObj1010" },
        { 0x00B6, "Cvtpgd1610" },
        { 0x00B7, "Cvtres1010" },
        { 0x00B8, "Export1010" },
        { 0x00B9, "Implib1010" },
        { 0x00BA, "Linker1010" },
        { 0x00BB, "Masm1010" },
        { 0x00BC, "Utc1610_C" },
        { 0x00BD, "Utc1610_CPP" },
        { 0x00BE, "Utc1610_CVTCIL_C" },
        { 0x00BF, "Utc1610_CVTCIL_CPP" },
        { 0x00C0, "Utc1610_LTCG_C" },
        { 0x00C1, "Utc1610_LTCG_CPP" },
        { 0x00C2, "Utc1610_LTCG_MSIL" },
        { 0x00C3, "Utc1610_POGO_I_C" },
        { 0x00C4, "Utc1610_POGO_I_CPP" },
        { 0x00C5, "Utc1610_POGO_O_C" },
        { 0x00C6, "Utc1610_POGO_O_CPP" },
        { 0x00C7, "AliasObj1100" },
        { 0x00C8, "Cvtpgd1700" },
        { 0x00C9, "Cvtres1100" },
        { 0x00CA, "Export1100" },
        { 0x00CB, "Implib1100" },
        { 0x00CC, "Linker1100" },
        { 0x00CD, "Masm1100" },
        { 0x00CE, "Utc1700_C" },
        { 0x00CF, "Utc1700_CPP" },
        { 0x00D0, "Utc1700_CVTCIL_C" },
        { 0x00D1, "Utc1700_CVTCIL_CPP" },
        { 0x00D2, "Utc1700_LTCG_C" },
        { 0x00D3, "Utc1700_LTCG_CPP" },
        { 0x00D4, "Utc1700_LTCG_MSIL" },
        { 0x00D5, "Utc1700_POGO_I_C" },
        { 0x00D6, "Utc1700_POGO_I_CPP" },
        { 0x00D7, "Utc1700_POGO_O_C" },
        { 0x00D8, "Utc1700_POGO_O_CPP" },
        { 0x00D9, "AliasObj1200" },
        { 0x00DA, "Cvtpgd1800" },
        { 0x00DB, "Cvtres1200" },
        { 0x00DC, "Export1200" },
        { 0x00DD, "Implib1200" },
        { 0x00DE, "Linker1200" },
        { 0x00DF, "Masm1200" },
        { 0x00E0, "Utc1800_C" },
        { 0x00E1, "Utc1800_CPP" },
        { 0x00E2, "Utc1800_CVTCIL_C" },
        { 0x00E3, "Utc1800_CVTCIL_CPP" },
        { 0x00E4, "Utc1800_LTCG_C" },
        { 0x00E5, "Utc1800_LTCG_CPP" },
        { 0x00E6, "Utc1800_LTCG_MSIL" },
        { 0x00E7, "Utc1800_POGO_I_C" },
        { 0x00E8, "Utc1800_POGO_I_CPP" },
        { 0x00E9, "Utc1800_POGO_O_C" },
        { 0x00EA, "Utc1800_POGO_O_CPP" },
        { 0x00EB, "AliasObj1210" },
        { 0x00EC, "Cvtpgd1810" },
        { 0x00ED, "Cvtres1210" },
        { 0x00EE, "Export1210" },
        { 0x00EF, "Implib1210" },
        { 0x00F0, "Linker1210" },
        { 0x00F1, "Masm1210" },
        { 0x00F2, "Utc1810_C" },
        { 0x00F3, "Utc1810_CPP" },
        { 0x00F4, "Utc1810_CVTCIL_C" },
        { 0x00F5, "Utc1810_CVTCIL_CPP" },
        { 0x00F6, "Utc1810_LTCG_C" },
        { 0x00F7, "Utc1810_LTCG_CPP" },
        { 0x00F8, "Utc1810_LTCG_MSIL" },
        { 0x00F9, "Utc1810_POGO_I_C" },
        { 0x00FA, "Utc1810_POGO_I_CPP" },
        { 0x00FB, "Utc1810_POGO_O_C" },
        { 0x00FC, "Utc1810_POGO_O_CPP" },
        { 0x00FD, "AliasObj1400" },
        { 0x00FE, "Cvtpgd1900" },
        { 0x00FF, "Cvtres1400" },
        { 0x0100, "Export1400" },
        { 0x0101, "Implib1400" },
        { 0x0102, "Linker1400" },
        { 0x0103, "Masm1400" },
        { 0x0104, "Utc1900_C" },
        { 0x0105, "Utc1900_CPP" },
        { 0x0106, "Utc1900_CVTCIL_C" },
        { 0x0107, "Utc1900_CVTCIL_CPP" },
        { 0x0108, "Utc1900_LTCG_C" },
        { 0x0109, "Utc1900_LTCG_CPP" },
        { 0x010A, "Utc1900_LTCG_MSIL" },
        { 0x010B, "Utc1900_POGO_I_C" },
        { 0x010C, "Utc1900_POGO_I_CPP" },
        { 0x010D, "Utc1900_POGO_O_C" },
        { 0x010E, "Utc1900_POGO_O_CPP" },
    };

    /// <summary> Symbolic name of a Rich product id ("Unknown" when unmapped). </summary>
    public static string Name(ushort productId)
        => Names.GetValueOrDefault(productId, "Unknown");

    /// <summary>
    /// Toolkit family derived from the tool name. Never throws.
    /// </summary>
    public static string Toolkit(ushort productId)
    {
        var name = Name(productId);

        if (productId == 1) return "Import";
        if (name.StartsWith("VisualBasic")) return "Visual Basic";
        if (name.StartsWith("Utc")) return "MSVC Compiler";
        if (name.StartsWith("Masm")) return "MASM";
        if (name.StartsWith("Linker")) return "MSVC Linker";
        if (name.StartsWith("Implib") || name.StartsWith("Export")) return "MSVC Library";
        if (name.StartsWith("Cvtres") || name == "Resource900") return "CVTRES";
        if (name.StartsWith("Cvtomf")) return "CVTOMF";
        if (name.StartsWith("Cvtpgd")) return "CVTPGD";
        if (name.StartsWith("AliasObj")) return "ALIASOBJ";
        if (name.StartsWith("ILAsm")) return ".NET IL Assembler";
        if (name == "Unknown") return "Unknown";

        return "Other";
    }

    /// <summary>
    /// Best-effort Visual Studio release for a product id, derived from the
    /// well-known contiguous id ranges. Returns an empty string when unknown.
    /// </summary>
    public static string VisualStudioVersion(ushort productId)
    {
        if (productId > 0x010E) return string.Empty;

        if (productId >= 0x00FD) return "Visual Studio 2015 (14.00)";
        if (productId >= 0x00EB) return "Visual Studio 2013 (12.10)";
        if (productId >= 0x00D9) return "Visual Studio 2013 (12.00)";
        if (productId >= 0x00C7) return "Visual Studio 2012 (11.00)";
        if (productId >= 0x00B5) return "Visual Studio 2010 (10.10)";
        if (productId >= 0x0098) return "Visual Studio 2010 (10.00)";
        if (productId >= 0x0083) return "Visual Studio 2008 (9.00)";
        if (productId >= 0x006D) return "Visual Studio 2005 (8.00)";
        if (productId >= 0x005A) return "Visual Studio 2003 (7.10)";
        if (productId == 0x0001) return "Visual Studio (import)";
        if (productId == 0x0000) return "Unknown";

        // Everything below 0x5A belongs to the legacy VC4/VC5/VC6 toolchain.
        return "Visual C++ 6.0 or earlier";
    }
}