using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Le;

namespace SunFlower.Le.Visualizers;

public class LeHeaderVisualizer : AbstractStructVisualizer<LeHeader>
{
    public LeHeaderVisualizer(LeHeader @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "Linear Executable Header",
            "Was used by Microsoft and IBM for containers of 16 and 32 bit code" +
            "Usually for Windows 3x and Windows 9x they were Virtual Device Drivers" + 
            "For MS-DOS they were Watcom and DOS4G/W extenders and for first 32-bit OS/2 it was main format of executables and DLLs" +
            "This format firstly appears in **Microsoft OS/2 2.0** (MS OS/2 differs with IBM release) developer previews and SDK." +
            "In Windows debugger's API this format commited like \"VxD executable\" or `exe_vxd`.",
            ToDataTable()
        );
    }
}