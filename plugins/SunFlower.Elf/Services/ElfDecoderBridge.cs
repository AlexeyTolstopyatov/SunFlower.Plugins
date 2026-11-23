// CoffeeLake (C) 2026-*
//
// ELF i386/amd64 bridge over Sunflower.Dasm.
// Mirrors SunFlower.Pe.Services.PeDecoderBridge but stays inside the ELF plugin
// so the ELF decoder does not pull the whole PE/LE stack.
//

using Sunflower.Dasm.Intel.Core;
using Decoder = Sunflower.Dasm.Intel.Core.Decoder;

namespace SunFlower.Elf.Services;

using DecodeResult = (IReadOnlyList<DecodedInstruction> instrs, byte[] status, SortedSet<int> entries);

public static class ElfDecoderBridge
{
    public static DecodeResult Decode32(byte[] bytes, int[] entries)
    {
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "Interrupt" , "linux86.json");

        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.I386);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }

    public static string FormatOperandPart(ProcessorMode mode, int instrOffset, DecodedOperand op) =>
        Decoder.formatOperandPart(mode, instrOffset, op.Part);
}