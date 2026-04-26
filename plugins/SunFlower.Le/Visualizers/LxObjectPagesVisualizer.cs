using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Visualizers;

public class LxObjectPagesVisualizer(List<ObjectPage> @struct) : AbstractStructVisualizer<List<ObjectPage>>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "Object Pages",
            "The Object page table provides information about a logical \n " +
            "page in  an object.  A  logical  page  may be  an enumerated \n " +
            "page, a pseudo page or an iterated  page. ",
            ToDataTable());
    }
}