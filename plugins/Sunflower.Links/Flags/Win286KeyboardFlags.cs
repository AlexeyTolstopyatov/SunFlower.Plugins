// CoffeeLake (C) 2026-*
// 
// The Win286KeyboardFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

[Flags]
public enum Win286KeyboardFlags : ushort
{
    NoAltTab = 0x0001,
    NoAltEsc = 0x0002,
    NoAltPrint = 0x0004,
    NoPrint = 0x0008,
    NoCtrlEsc = 0x0010,
    NoKeepScreen = 0x0020,
    DirectCOM3Access = 0x4000,
    DirectCOM4Access = 0x8000,
}