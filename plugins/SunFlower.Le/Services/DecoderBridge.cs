// CoffeeLake (C) 2026-*
// 
// DecoderBridge.cs 
// 
// @local_machine: atvlg
// @creator: atolstopyatov2017@vk.com

using Sunflower.Dasm.Intel.Core;
using Decoder = Sunflower.Dasm.Intel.Core.Decoder;

namespace SunFlower.Le.Services;

using DecodeResult = (IReadOnlyList<DecodedInstruction> instrs, byte[] status, SortedSet<int> entries);

public static class DecoderBridge
{
    public static DecodeResult Decode16(byte[] bytes, int[] entries)
    {
        var state = Decoder.create(I386MapPath, DosProgramInterruptsMapPath, ProcessorMode.I286);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }

    public static DecodeResult Decode32(byte[] bytes, int[] entries)
    {
        var state = Decoder.create(I386MapPath, DosProgramInterruptsMapPath, ProcessorMode.I386);
        var (list, _, entrySet) = Decoder.runRecursive(state, bytes, entries);
        return (list, [], new SortedSet<int>(entrySet));
    }
    
    public static string FormatOperandPart(ProcessorMode mode, int instrOffset, DecodedOperand op) =>
        Decoder.formatOperandPart(mode, instrOffset, op.Part);
    /// <summary>
    /// Despite the fact that 16-bit objects are contain pure i286 opcode + executes using
    /// "virtual 86" technology, for better support I use 386 opcode map + revert default operand size.
    ///
    /// This idea was born in IDA when I've seen module memory mages and captured that OPERAND_SIZE prefix
    /// changes. Standing in 32-bit to be 16-bit and standing in 16-bit page for to be 32-bit.
    /// 
    /// x86 Decoder uses CpuMode enum to set up correct filters/operand characteristics.
    /// </summary>
    private static string I386MapPath =>
        Path.Combine(AppContext.BaseDirectory, "opcodes386.json");
    /// <summary>
    /// (PC/MS)-DOS compatibility layer was placed in DOSCALLS virtual DLL, so many images   
    /// (mostly hand-made from .x86 sources) are based on program interrupts. 
    /// 
    /// Of course, it mostly causes the Win16/DOS applications so, 
    /// (I don't sure but Windows VxDs are use DOS program interrupts), I stay it here.
    /// Physical/Virtual OS/2 Drivers are based on another architecture. (see. IBM_OS2_KERNELDRIVER_GUIDE.pdf)
    /// </summary>
    private static string DosProgramInterruptsMapPath => 
        Path.Combine(AppContext.BaseDirectory, "Interrupt", "dos.json");

}