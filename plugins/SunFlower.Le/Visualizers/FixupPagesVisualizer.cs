using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Visualizers;

public class FixupPagesVisualizer(List<FixupPageRecord> @struct) : AbstractStructVisualizer<List<FixupPageRecord>>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "## Fixup Pages", 
            "The Fixup Page Table provides a simple mapping of a logical \n " +
            "page number to an offset into  the Fixup Record Table for that page.",
            ToDataTable());
    }
}