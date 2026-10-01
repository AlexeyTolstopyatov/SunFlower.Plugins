//
// CoffeeLake (C) 2026-*
// 
// DosExecutableFlower.cs
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//
using SunFlower.Abstractions;
using Sunflower.Mz.Models;
using Sunflower.Mz.Services;

namespace Sunflower.Mz;

public struct ProgramFilePointers
{
    public long CodeOffset;
    public long EntryPoint;
    public long StackOffset;
    public long StackPoint;
}

[Flower(FlowerTarget.Data)]
[FlowerVersionContract(5, 0,0)]
public class DosExecutableFlower : IFlower
{
    [Seed(
        name: "MZ Executable Header",
        description:
        "Relocatable executable which was used in MS-DOS 2+, PC-DOS 2+, other x86 disk operating systems.\r\n" +
        ""
    )]
    public MzHeader Header { get; private set; }
    
    [Seed(
        name: "File Pointers",
        description: "Custom summary which describes regions of file image"
    )]
    public ProgramFilePointers FilePointers { get; private set; }

    [Seed(
        name: "MZ Relocation Table",
        description: "Relocation table represents a sequence of far pointers"
    )]
    public MzRelocation[] Relocations { get; private set; }
    
    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        MzDumpManager manager = new(filePath);
        
        Header = manager.Header;
        Relocations = manager.Relocations.ToArray();
        FilePointers = new ProgramFilePointers()
        {
            CodeOffset = manager.CodeOffset,
            EntryPoint = manager.CodePointer,
            StackOffset = manager.StackOffset,
            StackPoint = manager.StackPointer
        };
    });
    
    public string Name => "Dump Mark Zbikowski Executable (MZ)";
}