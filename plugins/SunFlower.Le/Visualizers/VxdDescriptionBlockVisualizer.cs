using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Le.Headers.Le;

namespace SunFlower.Le.Visualizers;

public class VxdDescriptionBlockVisualizer : AbstractStructVisualizer<VxdDescriptionBlock>
{
    public VxdDescriptionBlockVisualizer(VxdDescriptionBlock @struct) : base(@struct)
    {
    }

    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override Region ToRegion()
    {
        return new Region(
            "### VxD: Description Block",
            "The device declaration block describes the virtual device to the VMM. \r\n" +
            "It provides a VxD mnemonic, usually a somewhat descriptive title using V as the prefix and D \r\n as " +
            "the suffix, such as VXFERD, suggesting a virtual transfer driver. \r\n" +
            "It also provides a major and minor version, the main control procedure, \r\n" +
            "the device ID number, the initialization order, \r\n" +
            "and control procedures for the V86 or Protected-Mode (PM) API:\r\n",
            ToDataTable());
    }
}