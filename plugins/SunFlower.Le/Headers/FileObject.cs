// CoffeeLake (C) 2026-*
// 
// The FileObject.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Le.Headers;

public struct FileObject(
    uint virtualSegmentSize,
    uint relocationBase,
    uint objectFlagsMask,
    uint pageMap,
    uint pageMapEntries,
    uint unknown)
{
    public uint VirtualSegmentSize = virtualSegmentSize;
    public uint RelocationBaseAddress = relocationBase;
    public uint ObjectFlagsMask = objectFlagsMask;
    public uint PageMapIndex = pageMap;
    public uint PageMapEntries = pageMapEntries;
    public uint Unknown = unknown; // or Reserved
}