// CoffeeLake (C) 2026-*
// 
// The RomDumpManager.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Services;

public class RomDumpManager : UnsafeManager
{
    public RomDumpManager(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        // Tests environment:
        // Windows 2000 SP0
        // Windows CE 2.11 SDK (vmware Player)
        // Embedded Visual C++
    }
}