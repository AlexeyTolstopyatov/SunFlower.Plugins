// CoffeeLake (C) 2026-*
//
// The RichManager extracts the Microsoft Rich header - the compiler
// fingerprint that the MSVC linker writes between the DOS stub and the PE
// signature. The records are XOR-encoded with a key stored right after the
// "Rich" marker; that key doubles as a checksum of the DOS header.
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

public class RichManager : UnsafeManager
{
    private const int DwordSize = 4;

    public RichHeaderModel RichHeader { get; } = new();

    /// <param name="path">PE image path</param>
    /// <param name="offset">DOS header field pointing at the PE signature</param>
    public RichManager(string path, long offset)
    {
        Parse(path, offset);
    }

    /// <summary>
    /// Reads the DOS stub area and decodes the Rich header. Never throws:
    /// a damaged / absent header just leaves <see cref="RichHeaderModel.Present"/>
    /// false (or partially filled).
    /// </summary>
    private void Parse(string path, long coffOffset)
    {
        // The Rich header always lives between the DOS stub and e_lfanew.
        if (coffOffset <= RichConstants.DosStubOffset || coffOffset > int.MaxValue)
            return;

        byte[] region;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            region = new byte[(int)coffOffset];
            stream.ReadExactly(region, 0, region.Length);
        }
        catch
        {
            return;
        }

        // .NET images produced by Roslyn carry no Rich header at all.
        var richIndex = IndexOf(region, RichConstants.RichSignature, RichConstants.DosStubOffset);
        if (richIndex < 0 || richIndex + DwordSize >= region.Length)
            return;

        RichHeader.Present = true;

        // The 32-bit word right after [Rich] is the XOR key.
        var key = BitConverter.ToUInt32(region, richIndex + DwordSize);
        RichHeader.Key = key;

        // Walk backwards from just before "Rich", XOR-decoding until "DanS".
        var decoded = new List<uint>();
        var dansIndex = -1;
        for (var offset = richIndex - DwordSize; offset >= RichConstants.DosStubOffset; offset -= DwordSize)
        {
            var value = BitConverter.ToUInt32(region, offset) ^ key;
            if (value == RichConstants.DansSignature)
            {
                dansIndex = offset;
                break;
            }

            decoded.Add(value);
        }

        // [Rich] present but DanS missing -> truncated header.
        if (dansIndex < 0)
            return;

        RichHeader.DansOffset = dansIndex;
        decoded.Reverse();

        // The three words that follow the DanS are zero in the BASED header.
        var start = 0;
        if (decoded.Count >= RichConstants.PaddingCount)
        {
            RichHeader.PaddingValid =
                decoded[0] == 0 && decoded[1] == 0 && decoded[2] == 0;
            start = RichConstants.PaddingCount;
        }

        // Records are (comp.id, usage count) pairs.
        for (var i = start; i + 1 < decoded.Count; i += 2)
        {
            var compId = decoded[i];
            var productId = (ushort)(compId >> 16);
            var buildNumber = (ushort)(compId & 0xFFFF);

            RichHeader.Items.Add(new RichHeaderItem
            {
                ProductId = productId,
                BuildNumber = buildNumber,
                Count = decoded[i + 1],
                Name = RichConstants.Name(productId),
                Toolkit = RichConstants.Toolkit(productId),
                VisualStudio = RichConstants.VisualStudioVersion(productId)
            });
        }

        RichHeader.ComputedChecksum = ComputeChecksum(region, dansIndex, RichHeader.Items);
        RichHeader.ChecksumValid = RichHeader.ComputedChecksum == key;
    }

    /// <summary>
    /// Recomputes the Rich checksum / key: every byte of the DOS header and
    /// stub (e_lfanew excluded) is rotated left by its offset and summed, then
    /// every comp.id is rotated left by its usage count and added.
    /// </summary>
    private static uint ComputeChecksum(byte[] region, int dansOffset, List<RichHeaderItem> items)
    {
        var checksum = (uint)dansOffset;

        for (var i = 0; i < dansOffset; i++)
        {
            // The e_lfanew field is excluded.
            if (i is >= 0x3C and <= 0x3F)
                continue;

            checksum += Rotate(region[i], i % 32);
        }

        foreach (var item in items)
        {
            var compId = ((uint)item.ProductId << 16) | item.BuildNumber;
            checksum += Rotate(compId, (int)(item.Count % 32));
        }

        return checksum;
    }

    private static uint Rotate(uint value, int shift)
    {
        shift &= 31;
        return shift == 0 ? value : (value << shift) | (value >> (32 - shift));
    }

    /// <summary>
    /// Finds a little-endian signature inside <paramref name="data"/>.
    /// </summary>
    private static int IndexOf(byte[] data, uint sign, int start)
    {
        var b0 = (byte)(sign & 0xFF);
        var b1 = (byte)((sign >> 8) & 0xFF);
        var b2 = (byte)((sign >> 16) & 0xFF);
        var b3 = (byte)((sign >> 24) & 0xFF);

        for (var i = start; i + DwordSize <= data.Length; i++)
        {
            if (data[i] == b0 && data[i + 1] == b1 && data[i + 2] == b2 && data[i + 3] == b3)
                return i;
        }

        return -1;
    }
}