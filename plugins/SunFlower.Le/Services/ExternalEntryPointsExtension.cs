// CoffeeLake (C) 2026-*
// 
// The ExternalEntryPointsExtension.cs represents little decoder sub-routines
// to insert defined Forwarder entry points of Linear Executable Objects
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public static class ExternalEntryPointsExtension
{
    /// <summary>
    /// Looks in the EntryTable, excludes all non-Forwarder entry points
    /// then inserts in the top of file the extern expressions
    /// </summary>
    public static string[] AddSignatures(in Entry[] enties) => enties
        .Where(x => x.Type == EntryBundleType.Forwarder)
        .Cast<EntryForwarder>()
        .Select(f => $"EXTERN \"C\" {f.EntryName}")
        .ToArray();
}