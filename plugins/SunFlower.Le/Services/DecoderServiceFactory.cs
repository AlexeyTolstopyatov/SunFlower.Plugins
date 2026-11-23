// CoffeeLake (C) 2026-*
//
// Factory for LE/LX decoder services.
// Reads the MZ + LE/LX header magic to decide which concrete decoder to instantiate,
// then wires up the matching DumpManager + DecoderService pair.
//
// Usage:
//
//   var decoder = DecoderServiceFactory.Create(filePath);
//   var listing = decoder.Disassemble();

using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public static class DecoderServiceFactory
{
    /// <summary>
    /// Accepts analysis summary and destination path of Linear Executable
    /// matching <see cref="DecoderServiceBase"/>.
    /// </summary>
    public static DecoderServiceBase Create(string dest, LeDumpManager manager)
    {
        // Trivial Windows virtual device driver contains _DDB export record
        // which represents a plain data instead of a function prologue.
        // 
        // Then pseudocode generation "silently fail" and interpret bytes as x86 instructions
        if (manager.IsDriver)
        {
            // Remove or mark entry point as unused
            manager.EntryBundles[manager.VxdDescriptorBlockBundleIndex].Entries[manager.VxdDescriptorBlockEntryIndex] 
                = new EntryUnused();
        }
        return new LeDecoderService(dest, manager);
    }
    /// <summary>
    /// Accepts analysis summary and destination path of Linear Executable
    /// matching <see cref="DecoderServiceBase"/>.
    /// </summary>
    public static DecoderServiceBase Create(string dest, LxDumpManager manager)
    {
        return new LxDecoderService(dest, manager);
    }
}