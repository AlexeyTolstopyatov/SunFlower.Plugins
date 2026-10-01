// CoffeeLake (C) 2026-*
// 
// The FileDosFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

[Flags]
public enum FileDosFlags : ushort
{
    DirectMemoryAccess = 0x0001,
    GraphicMode = 0x0002,
    PreventProgramSwith = 0x0004,
    NoScreenExchange = 0x0008,
    CloseWindowHostOnExit = 0x0010,
    DirectCOM1Access = 0x0020,
    DirectCOM2Access = 0x0040,
}