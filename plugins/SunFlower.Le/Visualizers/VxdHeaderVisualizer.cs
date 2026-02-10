using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Le;

namespace SunFlower.Le.Visualizers;

public class VxdHeaderVisualizer : AbstractStructVisualizer<VxdHeader>
{
    public VxdHeaderVisualizer(VxdHeader @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### Windows Virtual Device Driver",
            "VxD is the device driver model used in Microsoft Windows/386 2.x," +
            "the 386 enhanced mode of Windows 3.x, Windows 9x,\r\n and to some extent also by the Novell DOS 7, " +
            "OpenDOS 7.01, and DR-DOS 7.02+ multitask manager (TASKMGR). " +
            "\r\n" +
            "VxDs have access to the memory of the kernel and all running processes\r\n, as well as raw access to the hardware. \r\n" +
            "Starting with Windows 98, Windows Driver Model was the recommended driver model to write drivers for,\r\n " +
            "with the VxD driver model still being supported for backward compatibility\r\n, until Windows Me.\r\n",
            ToDataTable()
        );
    }
}