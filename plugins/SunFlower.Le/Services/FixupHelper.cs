// CoffeeLake (C) 2026-*
// 
// The FixupHelper.cs represents <what?>
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

namespace SunFlower.Le.Services;

using System.Text;

internal static class FixupDebug
{
    public static readonly bool Enabled = true;
        //Environment.GetEnvironmentVariable("LE_FIXUP_DEBUG") != "0";

    public static void Log(string msg)
    {
        if (Enabled) Console.WriteLine(msg);
    }

    public static void DumpHex(BinaryReader reader, long start, int length, string label)
    {
        if (!Enabled) return;
        var saved = reader.BaseStream.Position;
        reader.BaseStream.Position = start;
        var bytes = reader.ReadBytes(length);
        reader.BaseStream.Position = saved;

        Log($"{label} @ 0x{start:X8} ({bytes.Length} bytes):");
        for (var i = 0; i < bytes.Length; i += 16)
        {
            var take = Math.Min(16, bytes.Length - i);
            var line = new StringBuilder();
            for (var j = 0; j < take; j++)
                line.Append(bytes[i + j].ToString("X2")).Append(' ');
            Log($"  {start + i:X8}: {line}");
        }
    }
}
