// CoffeeLake (C) 2026-*
// 
// The Win386DosModeFlags.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace Sunflower.Links.Flags;

[Flags]
public enum Win386DosModeFlags : uint
{
    PermitExitWhenActive = 0x00000001,
    ContinueInBackground = 0x00000002,
    ExclusiveMode = 0x00000004,
    FullScreenMode =  0x00000008,
    NoAltTab = 0x00000020,
    NoAltEsc = 0x00000040,
    NoAltSpace = 0x00000080,
    NoAltEnter =  0x00000100,
    NoPrint = 0x00000200,
    NoAltPrint = 0x00000400,
    NoCtrlEscape = 0x00000800,
    NoHMA = 0x00002000,
    UseShortcutKey = 0x00004000,
    LockedEMS = 0x00008000,
    LockedXMS = 0x00010000,
    FastPaste =  0x00020000,
    LockedApplicationMemory = 0x00040000,
    ProtectedMemory = 0x00080000,
    MinimizedWindow = 0x00100000,
    MaximizedWindow = 0x00200000,
    MsDosMode = 0x00400000,
    PreventWindowsDetection = 0x10000000,
    NoMsDosModeTransitionWarn = 0x20000000,
    DosModeTransitionAsk = 0x40000000,
}