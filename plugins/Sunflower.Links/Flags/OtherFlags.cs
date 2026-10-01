// CoffeeLake (C) 2026-*
// 
// The OtherFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

[Flags]
public enum OtherFlags : ushort
{
    DirectKeyboardAccess = 0x0010,
    CoprocessorRequired = 0x0020,
    BackgroundStoppable = 0x0040,
    DirectScreenAccess = 0x1000,
    ExchangeInterruptVec = 0x2000,
    ParamsInArgv = 0x4000,
}