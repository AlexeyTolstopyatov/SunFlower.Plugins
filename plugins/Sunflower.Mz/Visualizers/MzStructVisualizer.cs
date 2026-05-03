using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;

namespace Sunflower.Mz.Visualizers;

public class MzStructVisualizer(MzHeader @struct) : AbstractStructVisualizer<MzHeader>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override string ToString()
    {
        return @"Main data structure for PC-DOS 2.0+, MS-DOS 2.0+ programs, 
which stores initial expected values and requirements for running";
    }

    public override Region ToRegion()
    {
        return new Region("MZ Executable Header", ToString(), ToDataTable());
    }
}