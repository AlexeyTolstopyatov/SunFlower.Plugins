using System.Data;
using System.Text;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Visualizers;

public class PeClrStructVisualizer(Cor20Header @struct) : AbstractStructVisualizer<Cor20Header>(@struct)
{
    private readonly string _content = "CLR Header";
    
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        var content = new StringBuilder();
        content.AppendLine("This is a .NET assembly common information section. ");
        content.AppendLine(FlowerReport.ForColumn("SizeOfHead", typeof(int)) + " always equals 0x48 for the correct build .NET assemblies");
        content.AppendLine("This header tells that build has Microsoft `IL` instructions and all project metadata");
        
        return new Region(_content, content.ToString(), ToDataTable());
    }
}