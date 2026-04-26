using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Le;

namespace SunFlower.Le.Visualizers;

public class VxdResourcesHeaderVisualizer : AbstractStructVisualizer<VxdResources>
{
    public VxdResourcesHeaderVisualizer(VxdResources @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "VxD: Resources Heading", 
            "This block usually has Windows 95+ VxD drivers\r\n" +
            "(having .vxd extension). If this block contains zeros -- \r\n" +
            "resources which follows next are missing or not nested in this block. (see Linear Executable resources table.)\r\n", 
            ToDataTable());
    }
}