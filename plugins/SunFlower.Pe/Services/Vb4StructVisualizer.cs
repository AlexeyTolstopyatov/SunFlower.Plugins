using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Services;

public class Vb4StructVisualizer(Vb4Header @struct) : AbstractStructVisualizer<Vb4Header>(@struct)
{
    private readonly string _content = "### Visual Basic 4.0 Unofficial Section";
    private readonly Vb4Header _struct1 = @struct;

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct1);
    }

    public override string ToString()
    {
        return "This is a section that bases on the legacy by `VBGamer 45` and `DoDi` placed here." + 
               "I'm trying to demangle and define other undocumented leaked structure fields. for PE32 linked programs" +
               "So, If you see this section - you must know this is a very rare artifact.";
    }

    public override Region ToRegion()
    {
        return new Region(_content, ToString(), ToDataTable());
    }
}