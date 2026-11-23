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
    public static DecodeResult Decode32(byte[] bytes, int[] entries)
    {
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "dos.json");

        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.I386);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }

    public static string FormatOperandPart(ProcessorMode mode, int instrOffset, DecodedOperand op) =>
        Decoder.formatOperandPart(mode, instrOffset, op.Part);
}
