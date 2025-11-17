using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using Object = SunFlower.Le.Headers.Lx.Object;

namespace SunFlower.Le.Visualizers;

public class ObjectTableVisualizer : AbstractStructVisualizer<List<Object>>
{
    public ObjectTableVisualizer(List<Object> @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        var objectsTable = new DataTable();
        objectsTable.Columns.Add("#");
        objectsTable.Columns.Add("Name:s");
        objectsTable.Columns.Add("VirtualSize:4");
        objectsTable.Columns.Add("RelBase:4");
        objectsTable.Columns.Add("FlagsMask:4");
        objectsTable.Columns.Add("PageMapIndex:4");
        objectsTable.Columns.Add("PageMapEntries:4");
        objectsTable.Columns.Add("Unknown:4");
        objectsTable.Columns.Add("Flags:s");

        var counter = 1;
        foreach (var table in _struct)
        {
            var text = table
                .ObjectFlags
                .Aggregate("", (current, s) => current + $"`{s}` ");
            var name = Object.GetSuggestedNameByPermissions(table);
            
            objectsTable.Rows.Add(
                counter,
                name,
                "0x" + table.VirtualSegmentSize.ToString("X8"),
                "0x" + table.RelocationBaseAddress.ToString("X8"),
                "0x" + table.ObjectFlagsMask.ToString("X8"),
                "0x" + table.PageMapIndex.ToString("X8"),
                "0x" + table.PageMapEntries.ToString("X8"),
                table.Unknown.ToString("X8"),
                text
            );

            counter++;
        }

        return objectsTable;
    }

    public override Region ToRegion()
    {
        return new Region(
            "## Objects Table",
            "Objects defines a sections of code and data,\r\n what require to be placed" +
            "in specific memory allocation while module is loading.",
            ToDataTable());
    }
}