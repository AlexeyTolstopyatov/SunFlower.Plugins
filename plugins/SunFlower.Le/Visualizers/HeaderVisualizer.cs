using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Visualizers;

public class HeaderVisualizer : AbstractStructVisualizer<LxHeader>
{
    public HeaderVisualizer(LxHeader @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### Linear Executable Header",
            "Linear Executable is an executable file format in the EXE family. " +
            "It was used by 32-bit OS/2, by some DOS extenders, " +
            "and by Microsoft Windows VxD files. It is an extension of MS-DOS EXE, " +
            "and a successor to NE (New Executable)." +
            "There are two main varieties of it: LX (32-bit), and LE (mixed 16/32-bit).\r\n",
            ToDataTable()
        );
    }
}