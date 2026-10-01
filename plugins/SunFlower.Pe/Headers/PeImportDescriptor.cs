namespace SunFlower.Pe.Headers;

public struct PeImportDescriptor
{
    public uint OriginalFirstThunk;
    public uint TimeDateStamp;
    public uint ForwarderChain;
    public uint Name;
    public uint FirstThunk;
}