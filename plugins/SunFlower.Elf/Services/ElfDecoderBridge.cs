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
    public static DecodeResult DecodeX86(byte[] bytes, int[] entries)
    {
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "Interrupt" , "linux86.json");
        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.I386);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        
        return (list, [], new SortedSet<int>(entrySet));
    }

    public static DecodeResult DecodeX8664(byte[] bytes, int[] entries)
    {
        // TODO: replace linux86 to linux8664 syscall map
        var opcodeMap = Path.Combine(AppContext.BaseDirectory, "opcodes8664.json");
        var dosApi = Path.Combine(AppContext.BaseDirectory, "Interrupt" , "linux86.json");
        var state = Decoder.create(opcodeMap, dosApi, ProcessorMode.AMD64);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        
        return (list, [], new SortedSet<int>(entrySet));
    }
    
    /// <summary>
    ///     Выбирает режим дизассемблера в зависимости от требуемой архитектуры
    ///     (ELF32 → i386, ELF64 → amd64) и делегирует вызов соответствующему мосту.
    /// </summary>
    public static DecodeResult Decode(byte[] bytes, int[] entries, bool is64Bit) =>
        is64Bit ? DecodeX8664(bytes, entries) : DecodeX86(bytes, entries);

    public static string FormatOperandPart(ProcessorMode mode, int instrOffset, DecodedOperand op) =>
        Decoder.formatOperandPart(mode, instrOffset, op.Part);
}