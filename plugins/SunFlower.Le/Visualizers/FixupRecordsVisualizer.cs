using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers;

namespace SunFlower.Le.Visualizers;

public class FixupRecordsVisualizer(List<FixupRecord> @struct) : AbstractStructVisualizer<List<FixupRecord>>(@struct)
{
    public override DataTable ToDataTable()
    {
        return FlowerReflection.ListToDataTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### Fixup Records | Common Data",
            " The  Fixup  Record Table contains entries  for all fixups in \n " +
            "the linear EXE module.  The fixup records for a logical page \n " +
            "are  grouped together and kept  in  sorted order by  logical page number. \n " +
            "The  fixups for  each page are  further sorted such that all external fixups \n" +
            "and internal selector/pointer fixups come before internal non-selector/non-pointer fixups. \n " +
            "This allows  the  loader  to  ignore internal fixups  if the \n" +
            "loader  is  able  to  load  all  objects  at  the  addresses \n " +
            "specified in the object table. ",
            ToDataTable());
    }
}