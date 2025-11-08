namespace SunFlower.Le.Headers.Le;

public static class FixupRelocationType
{
    public static string GetRelocationType(byte rtp)
    {
        return (rtp >> 6) switch
        {
            0 => "INTERNAL_REF",
            1 => "IMPORT_ORDINAL",
            2 => "IMPORT_NAME",
            3 => "OSFIXUP",
            _ => "INVALID!"
        };
    }

    public static bool IsAdditive(byte rtp) => (rtp & 0x01) != 0;
    public static bool Is32BitTarget(byte rtp) => (rtp & 0x02) != 0;
    public static bool Is16BitOrdinal(byte rtp) => (rtp & 0x08) != 0;
    public static bool HasExtraData(byte rtp) => (rtp & 0x10) != 0;
}