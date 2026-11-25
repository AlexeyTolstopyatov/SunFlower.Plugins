// CoffeeLake (C) 2026-*
//
// PE i386 bridge over Sunflower.Dasm.
// Mirrors SunFlower.Le.Services.DecoderBridge but stays inside the PE plugin
// so the PE decoder does not pull the whole LE/LX stack.

using Sunflower.Dasm.Intel.Core;
using Decoder = Sunflower.Dasm.Intel.Core.Decoder;

namespace SunFlower.Pe.Services;

using DecodeResult = (IReadOnlyList<DecodedInstruction> instrs, byte[] status, SortedSet<int> entries);

public static class PeDecoderBridge
{
    private static DecodeResult Decode32(byte[] bytes, int[] entries)
    {
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "dos.json");

        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.I386);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }

    private static DecodeResult Decode64(byte[] bytes, int[] entries)
    {
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes8664.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "dos.json");

        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.AMD64);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }

    /// <summary>
    /// Calls various Sunflower.Dasm API depending on given architecture
    /// </summary>
    /// <param name="bytes">Bytes selection which will be decoded</param>
    /// <param name="entries">Possible offsets which will be accepted as entry points</param>
    /// <param name="is64Bit">TODO: Decoder API enum </param>
    public static DecodeResult Decode(byte[] bytes, int[] entries, bool is64Bit) =>
        is64Bit ? Decode64(bytes, entries) : Decode32(bytes, entries);

    public static string FormatOperandPart(ProcessorMode mode, int instrOffset, DecodedOperand op) =>
        Decoder.formatOperandPart(mode, instrOffset, op.Part);
}
