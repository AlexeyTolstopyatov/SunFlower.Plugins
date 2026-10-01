// CoffeeLake (C) 2026-*
// 
// The VideoPageFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

[Flags]
public enum VideoPageFlags
{
    // lastviopage# = x & 0x0007;
    Text,
    Graphic = 0x0010
}