using System.Data;
using System.Text;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Visualizers;

public class Pe32StructVisualizer(PeOptionalHeader32 @struct) : AbstractStructVisualizer<PeOptionalHeader32>(@struct)
{
    private readonly string _content = "Optional `PE32` Header";
    
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        var content = new StringBuilder();
        content.AppendLine(
            "This header named \"optional\" because not for all PE linked objects this header must been");
        content.AppendLine("Depends on `Magic` field defines maximum bounds for heap and stack characteristics");
        content.AppendLine("Some data in this header not checks by loader in the runtime -> can be changed manually");
        
        return new Region(_content, content.ToString(), ToDataTable());
    }
}