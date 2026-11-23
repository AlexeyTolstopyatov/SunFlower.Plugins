// CoffeeLake (C) 2026-*
//
// The TlsDirectoryModel.cs represents the TLS data directory (9).
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Models;

/// <summary>
/// Thread Local Storage directory. Beyond the TLS template it exposes the
/// callback array: those callbacks run BEFORE the entry point, so a
/// disassembler must treat them as additional entry points.
/// The raw address fields are Virtual Addresses (as stored in the image);
/// the <c>*Rva</c> helpers are computed after subtracting the ImageBase.
/// </summary>
public class TlsDirectoryModel
{
    /// <summary> True when the TLS directory is present and was readable. </summary>
    public bool Present { get; set; }

    /// <summary> Start of the initialized TLS template (VA). </summary>
    public ulong StartAddressOfRawData { get; set; }

    /// <summary> End of the initialized TLS template (VA). </summary>
    public ulong EndAddressOfRawData { get; set; }

    /// <summary> Address of the TLS index slot (VA). </summary>
    public ulong AddressOfIndex { get; set; }

    /// <summary> Address of the null-terminated callback pointer array (VA). </summary>
    public ulong AddressOfCallBacks { get; set; }

    /// <summary> Bytes of zero fill following the template. </summary>
    public uint SizeOfZeroFill { get; set; }

    /// <summary> Raw characteristics (low nibble = alignment). </summary>
    public uint Characteristics { get; set; }

    // RVAs derived from the Virtual Addresses above (0 when not applicable).
    public uint StartRva { get; set; }
    public uint EndRva { get; set; }
    public uint IndexRva { get; set; }
    public uint CallBacksRva { get; set; }

    /// <summary>
    /// TLS callbacks as Virtual Addresses, in image order. These execute before
    /// the entry point, so they are entry points for a disassembler.
    /// </summary>
    public List<ulong> Callbacks { get; set; } = [];
}