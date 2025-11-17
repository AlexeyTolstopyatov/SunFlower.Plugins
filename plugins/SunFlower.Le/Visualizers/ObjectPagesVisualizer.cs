using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Models.Le;

namespace SunFlower.Le.Visualizers;

public class ObjectPagesVisualizer : AbstractStructVisualizer<List<ObjectPageModel>>
{
    public ObjectPagesVisualizer(List<ObjectPageModel> @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct.Select(s => s.Page));
    }

    public override Region ToRegion()
    {
        return new Region(
            "## Object Pages",
            "The Object page table provides information about a logical \n " +
            "page in  an object.  A  logical  page  may be  an enumerated \n " +
            "page, a pseudo page or an iterated  page. ",
            ToDataTable()
        );
    }
}