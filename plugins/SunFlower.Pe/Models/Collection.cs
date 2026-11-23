// CoffeeLake (C) 2026-*
// 
// The Collection.cs represents little magic
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Pe.Models;


public class ImportedFunctionCollection : List<ImportFunction>
{
    public override string ToString()
    {
        return $"({Count} procedures)";
    }
}

public class ExportedFunctionCollection : List<ExportFunction>
{
    public override string ToString()
    {
        return $"({Count} exports)";
    }
}

public class RichToolkitCollection : List<RichHeaderItem>
{
    public override string ToString()
    {
        return $"{Count} kits";
    }
}