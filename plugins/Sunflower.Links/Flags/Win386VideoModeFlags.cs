// CoffeeLake (C) 2026-*
// 
// The Win386VideoModeFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

public enum Win386VideoModeFlags : ushort
{
    VideoRomEmulation = 0x0001,
    PortText = 0x0002,
    PortLoGrahics = 0x0004,
    PortHiGrahics = 0x0008,
    VideoText = 0x0010,
    VideoLoGrahics = 0x0020,
    VideoHiGrahics = 0x0040,
    RetainVideoMemory = 0x0080,
}