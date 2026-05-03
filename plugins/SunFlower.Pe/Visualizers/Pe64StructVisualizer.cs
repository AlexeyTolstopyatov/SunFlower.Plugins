using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Visualizers;

public class Pe64StructVisualizer(PeOptionalHeader @struct) : AbstractStructVisualizer<PeOptionalHeader>(@struct)
{
    private readonly string _content = "Optional `PE32+` (64-bit) Header";
    public override DataTable ToDataTable()
    {
        return FlowerReflection.GetNameValueTable(_struct);
    }

    public override string ToString()
    {
        return @"This header names optional because isn't necessary for all PE linked files.
Depends on `wMagic`
field defines maximum word size for HEAP and STACK characteristics.
Some data don't check by loader and may be empty or incorrect.";
    }

    public override Region ToRegion()
    {
        return new Region(_content, ToString(), ToDataTable());
    }
}