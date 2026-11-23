// CoffeeLake (C) 2026-*
// 
// The PeBaseRelocationManager.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Pe.Headers;
using SunFlower.Pe.Models;

namespace SunFlower.Pe.Services;

public class PeBaseRelocationManager : DirectoryManager
{
    private readonly ImageDetails _info;
    public readonly List<uint> RelocatedVas = [];
    public readonly  List<ulong> RelocatedVas64 = [];
    private readonly string _path;

    public PeBaseRelocationManager(ImageDetails info, string path) : base(info)
    {
        _path = path;
        _info = info;
        
        Dump();
    }

    private void Dump()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        
        if (_info.Directories[5] is not { VirtualAddress: > 0, Size: > 0 } rel) 
            return;

        var start = Offset(rel.VirtualAddress);
        if (start < 0) 
            return;

        var end = start + rel.Size;
        reader.BaseStream.Seek(start, SeekOrigin.Begin);

        while (reader.BaseStream.Position < end)
        {
            var block = Fill<PeDirectory>(reader);
            if (block.Size < 8) break;

            var entries = (int)((block.Size - 8) / 2);
            for (var i = 0; i < entries; i++)
            {
                var entry = reader.ReadUInt16();
                var type  = entry >> 12;
                var ofs   = (uint)(entry & 0x0FFF);

                if (!_info.Is64Bit && type == 3)   // IMAGE_REL_BASED_HIGHLOW
                    RelocatedVas.Add(_info.ImageBase + block.VirtualAddress + ofs);
                else if (_info.Is64Bit && type == 10) // IMAGE_REL_BASED_DIR64
                    RelocatedVas64.Add(_info.ImageBase64 + block.VirtualAddress + ofs);
            }
        }
    }
}
