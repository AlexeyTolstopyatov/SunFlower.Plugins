using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Visualizers;

public class LxHeaderVisualizer(LxHeader @struct) : AbstractStructVisualizer<LxHeader>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### IBM Linear Executable Header",
            "Linear Executable is an executable file format in the EXE family. " +
            "It was used by 32-bit OS/2, by some DOS extenders, " +
            "This format is exactly IBM modified solution and a successor to NE (New Executable).\r\n",
            ToDataTable()
        );
    }
}