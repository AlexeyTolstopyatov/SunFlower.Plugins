using SunFlower.Abstractions;
using Sunflower.Links.Headers;
using SunFlower.Links.Services;

namespace Sunflower.Links;

/// <summary>
/// PIF binary is a "Program Information File"
/// contained link to existed .COM/.EXE file in old
/// DOS|Windows|OS/2 operating systems.
/// </summary>
[FlowerVersionContract(5, 0, 0)]
[Flower(FlowerTarget.Data)]
public class PifFlower : IFlower
{
    [Seed(
        name: "Microsoft PIF | Found Sections",
        description: "Microsoft program information file is a binary file \r\n" +
                     "which completely describes how DOS application could run under Windows.\r\n" +
                     "This format is deprecated since DOS virtual machine and DOS mode had been\r\n" +
                     "Mostly used in Windows 1.x, 2,x, 3.x by Program manager (file name: `PROGMAN,EXE`\r\n)" +
                     "or DOS Executive shell (file: `MSDOSD.EXE`)"
    )]
    public PifSectionHead[] Sections;

    [Seed(
        name: "Microsoft PifEx Section",
        description: "First format modification for Microsoft Windows 1.x.\n`Checksum=0x78` on Windows 9x"
    )]
    public MicrosoftPifEx MicrosoftPifEx;

    [Seed(
        name: "Microsoft Windows 3.x/286 Section",
        description: ""
    )]
    public Windows3x286 Windows3x286;

    [Seed(
        name: "Microsoft Windows 3.x/386 Section",
        description: ""
    )]
    public Windows3x386 Windows3x386;

    [Seed(
        name: "Microsoft Windows 9x Section",
        description: ""
    )]
    public Windows4xVmm Windows4xVmm;

    public Task CreateAsync(string filePath) => Task.Run(() =>
    {
        PifDumpManager manager = new(filePath);
        Sections = manager.SectionHeads.ToArray();
        MicrosoftPifEx = manager.MicrosoftPifEx;
        Windows3x286 = manager.Windows3X286;
        Windows3x386 = manager.Windows3X386;
        Windows4xVmm = manager.Windows4XVmm;
    });

    public string Name => "Dump MS-DOS PIF";
}