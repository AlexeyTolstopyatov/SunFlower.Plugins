namespace SunFlower.Le.Headers;

public struct ObjectPageOffsetPair(uint number, long offset)
{
    public uint Page = number;
    public long Offset = offset;
}