using System.Data;
using System.Text;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;
using SunFlower.Abstractions;

namespace SunFlower.Pe.Services;

public class Vb5StructVisualizer(Vb5Header @struct) : AbstractStructVisualizer<Vb5Header>(@struct)
{
    private readonly string _heading = "### Visual Basic 5.0/6.0 Runtime section";
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        var content = new StringBuilder();

        content.AppendLine("If you see this section - this is already PE32 linked binary with embedded Microsoft Visual Basic runtime.");
        content.AppendLine("This structure is a part of VB 5.0 or a VB 6.0 runtime. It depends on target DLL which `@100` requires to correct run.");
        content.AppendLine($" - VBVM ver details. `__.__.{_struct.RuntimeBuild}.{_struct.RuntimeRevision}`");
        content.AppendLine($" - VBVM DLL: {FlowerReport.SafeString(new string(_struct.LanguageDll))}");
        
        return new Region(_heading, content.ToString(), ToDataTable());
    }
}