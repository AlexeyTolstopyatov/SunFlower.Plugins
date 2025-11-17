using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Visualizers;

public class NonResidentNamesVisualizer : AbstractStructVisualizer<List<ExportRecord>>
{
    public NonResidentNamesVisualizer(List<ExportRecord> @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### NonResident Names | Public Exports",
            "Non-resident  names  are  not  kept in memory \r\n" +
            "and are read from the  EXE file when a  dynamic  link reference\r\n" +
            "is made.  Exported  entry  point  names  that are infrequently\r\n" +
            "dynamicaly linked to by  name  or are  commonly referenced\r\n" +
            "by  ordinal  number  should  be  placed  in  the \n non-resident name table.\r\n" +
            "The trade off made for references by name is performance vs memory usage.\r\n\r\n",
            ToDataTable()
        );
    }
}