namespace SunFlower.Le.Headers.Le;

public static class FixupAddressType
{
    public static string GetAddressType(byte atp)
    {
        return (atp >> 4) switch
        {
            0 => "LOW_BYTE",
            2 => "SELECTOR_16_BIT",
            3 => "FAR_32",
            5 => "OFFSET_16",
            6 => "FAR_48",
            7 => "OFFSET_32",
            8 => "OFFSET_REL_EIP_32",
            _ => "RESERVED"
        };
    }

    public static bool HasOffsetList(byte atp) => (atp & 0x02) != 0;
    public static bool Is16BitAlias(byte atp) => (atp & 0x01) != 0;
}