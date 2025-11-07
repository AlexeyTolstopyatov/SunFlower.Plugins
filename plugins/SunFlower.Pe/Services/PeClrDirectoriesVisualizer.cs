using System.Data;
using SunFlower.Abstractions;
using SunFlower.Abstractions.Types;
using SunFlower.Pe.Headers;

namespace SunFlower.Pe.Services;

public class PeClrDirectoriesVisualizer(List<PeDirectory> @struct)
    : AbstractStructVisualizer<List<PeDirectory>>(@struct)
{
    public override DataTable ToDataTable()
    {
        if (_struct is null)
        {
            return new DataTable();
        }

        return FlowerReflection.ListToDataTable(_struct);
    }

    public override string ToString()
    {
        return @"
Directories iterated in table:
 - `Resources`
 - `StrongName`
 - `CodeManager`
 - `VTableDirectory`
 - `Exports`
 - `ManagedNativeHeader`

";
    }

    public override Region ToRegion()
    {
        return new Region("### CLR Runtime Directories", ToString(), ToDataTable());
    }
}