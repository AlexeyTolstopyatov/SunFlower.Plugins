using System.Runtime.InteropServices;

namespace SunFlower.Ne.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NeSegmentInfo
{
    public ushort FileOffset;
    public ushort FileLength;
    public ushort Flags;
    public ushort MinAllocation;
    private string _type;

    // Segment type flags from NE format
    private const ushort SEGFLAGS_TYPE_MASK = 0x0007; // bits 0-2

    public string Type
    {
        get
        {
            var segmentType = (ushort)(Flags & SEGFLAGS_TYPE_MASK);
            _type = segmentType switch
            {
                0x0000 => ".CODE",  // Code segment
                0x0001 => ".DATA",  // Data segment
                0x0002 => ".DATA",  // ITERM - data segment with iter
                0x0003 => ".DATA",  // Data segment (OS/2)
                _ => ".CODE"        // Default to code
            };
            return _type;
        }
        set => _type = value;
    }
}