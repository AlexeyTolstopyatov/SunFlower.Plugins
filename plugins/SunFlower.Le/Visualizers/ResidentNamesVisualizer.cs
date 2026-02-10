using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Visualizers;

public class ResidentNamesVisualizer(List<ExportRecord> @struct) : AbstractStructVisualizer<List<ExportRecord>>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### Resident Names | ~~Private~~ Export Records",
            "The resident name table is kept resident in system memory \r\n" +
            "while the module is loaded. It is intended to contain  the \r\n " +
            "exported entry point names that are frequently dynamicaly \r\n " +
            "linked to by name.",
            ToDataTable()
        );
    }
}