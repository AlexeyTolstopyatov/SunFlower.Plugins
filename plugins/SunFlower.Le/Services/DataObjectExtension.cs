//
// CoffeeLake (C) 2026-*
//
// The DataObjectExtension.cs represents a byte-level scan of an LE/LX object
// (a collection of memory pages) which is not a part of the disassembler itself:
//     - the printable text of an object are translated into DB string literals
//     - the procedure prologues of a DATA object are spotted, so the code
//       hidden in the data becomes an entry point of the analysis
//
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com
//

using System.Text;

namespace SunFlower.Le.Services;

/// <summary>
/// Byte-level scan of an object image. Shared by the LE and LX decoders, so the
/// string dump and the "code in data" heuristic look the same for both formats.
/// </summary>
public static class DataObjectExtension
{
    /// <summary>
    /// Minimal length of a printable run reported as a string.
    /// </summary>
    public const int DefaultMinStringLength = 3;

    /// <summary>
    /// $UNDER CONSTRUCTION$
    /// How exactly Microsoft LNK386/Watcom linker places procedures?
    /// 
    /// The (default?) x86 procedure prologues used to spot the code inside a data object
    /// (little example):
    /// <code>
    ///     55 8B EC          push bp/ebp      ; mov bp/ebp, sp/esp
    ///     55 89 E5          push ebp         ; mov ebp, esp (32-bit)
    ///     8B FF 55 8B EC    mov edi, edi     ; push ebp ; mov ebp, esp (hot-patch)
    ///     C8 xx xx 00       enter imm16, 0
    /// </code>
    /// </summary>
    private static readonly byte[][] ProcedurePrologues =
    [
        [0x55, 0x8B, 0xEC],
        [0x55, 0x89, 0xE5],
        [0x8B, 0xFF, 0x55, 0x8B, 0xEC]
    ];

    /// <summary>
    /// Tells if a byte is an ASCII.
    /// </summary>
    private static bool IsPrintable(byte value) =>
        value is 0x09 or >= 0x20 and <= 0x7E;

    /// <summary>
    /// Collects the text of printable ASCII characters which are not shorter
    /// than <paramref name="minLength"/>.
    /// </summary>
    public static List<(int Offset, string Text)> CollectAsciiStrings(byte[] bytes, int minLength)
    {
        var result = new List<(int Offset, string Text)>();

        if (bytes.Length == 0 || minLength <= 0)
            return result;

        var builder = new StringBuilder();
        var start = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            if (IsPrintable(bytes[i]))
            {
                if (builder.Length == 0)
                    start = i;

                builder.Append((char)bytes[i]);
                continue;
            }

            if (builder.Length >= minLength)
                result.Add((start, builder.ToString()));

            builder.Clear();
        }

        if (builder.Length >= minLength)
            result.Add((start, builder.ToString()));

        return result;
    }

    /// <summary>
    /// Finds every offset of <paramref name="bytes"/> which starts with a known
    /// procedure prologue. The result is sorted and unique, so the offsets might
    /// be added to the entry points of the analysis right away.
    /// </summary>
    public static SortedSet<int> FindProcedurePrologues(byte[] bytes)
    {
        var result = new SortedSet<int>();

        for (var i = 0; i + 3 < bytes.Length; i++)
        {
            // enter imm16, imm8 - the nesting level is almost always zero
            if (bytes[i] is 0xC8 && bytes[i + 3] is 0x00)
                result.Add(i);

            if (ProcedurePrologues
                .Where(prologue => i + prologue.Length <= bytes.Length)
                .Any(prologue => bytes
                    .AsSpan(i, prologue.Length)
                    .SequenceEqual(prologue)))
            {
                result.Add(i);
            }
        }

        return result;
    }
}
