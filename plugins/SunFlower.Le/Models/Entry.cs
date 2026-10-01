// CoffeeLake (C) 2026-*
// 
// The Entry.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using SunFlower.Le.Headers;

namespace SunFlower.Le.Models;

public class Entry(int ordinal, string name, int obj, int offset, string? entryType, EntryBundleType bundleType)
{
    public int Ordinal { get; init; } = ordinal;
    public string? Name { get; init; } = name;
    public int Object { get; init; } = obj;
    public int Offset { get; init; } = offset;
    public EntryBundleType BundleType = bundleType;
    public string? EntryType = entryType;
}