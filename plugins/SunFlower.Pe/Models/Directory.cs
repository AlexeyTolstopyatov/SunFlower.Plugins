// CoffeeLake (C) 2026-*
// 
// The Directory.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Models;

public class Directory
{
    public Directory(uint va, uint sz, string name)
    {
        if (va > 0)
            VirtualAddress = va;
        if (sz > 0)
            Size = sz;

        Name = name;
    }

    public string? Name { get; private set; }
    public uint? VirtualAddress { get; private set; }
    public uint? Size { get; private set; }
}