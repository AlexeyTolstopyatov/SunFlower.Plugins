using SunFlower.Le.Headers;
using Sunflower.Mz.Services;

namespace LinearExecutable;

using Sunflower.Dasm;
[TestClass]
public class I8086Test
{
    [TestMethod]
    public void TestI8086TouchOperation()
    {
        // Expecting "ADD AL, AL" (reg, reg)
        var intel = I8086Decoder.get();
        var reg = I8086Decoder.touchOperation(intel, [0x00, 0xC0]); // "ADD AL, AL"
        // 2. ADD AL, immediate (без ModRM)
        var imm = I8086Decoder.touchOperation(intel, [0x04, 0x10]); // "ADD AL, 0x10"
        // 3. JMP short (Jb = -2, т.е. на себя)
        // Instruction capacity about 2 bytes, offset -2 bytes. (= IP + 2 - 2)
        // touchOperation : targetAddr = parserIdx + rel.
        // Expecting "JMP 0x0000". 
        var jmp = I8086Decoder.touchOperation(intel, [0xEB, 0xFE]); // "JMP 0000"
        // 4. Segment prefix test + MOV [BX], AL
        // 26 88 07 -> ES: MOV [BX], AL
        var movseg = I8086Decoder.touchOperation(intel, [0x26, 0x88, 0x07]); // "ES:MOV [BX], AL"
        // 5. MOV AX, imm16
        var mivImm = I8086Decoder.touchOperation(intel, [0xB8, 0x34, 0x12]); // "MOV AX, 0x1234"
        // 6. PUSH AX (opcode 50)
        var push = I8086Decoder.touchOperation(intel, [0x50]); // "PUSH AX"
        // 7. XOR AL, AL (30 C0)
        var xor = I8086Decoder.touchOperation(intel, [0x30, 0xC0]); // "XOR AL, AL"
        // 8. MOVS byte (A4)
        var movs = I8086Decoder.touchOperation(intel, [0xA4]); // "MOVSB" ?? "MOVS"
        // 9. INC AX (40)
        var inc = I8086Decoder.touchOperation(intel, [0x40]); // "INC AX"
        
        Console.WriteLine(reg);    // Some((ADD AL, AL, 2))
        Console.WriteLine(imm);    // Some((ADD AL, 0x10, 2))
        Console.WriteLine(jmp);    // Some((JMP 0x0000, 2))
        Console.WriteLine(movseg); // Some((ES:MOV [BX], AL, 3))
        Console.WriteLine(mivImm); // Some((MOV AX, 0x1234, 3))
        Console.WriteLine(push);   // Some((PUSH AX, 1))
        Console.WriteLine(xor);    // Some((XOR AL, AL, 2))
        Console.WriteLine(movs);   // Some((MOVSB, 1))
        Console.WriteLine(inc);    // Some((INC AX, 1))
    }
    
    [TestMethod]
    public void TestI8086Group1()
    {
        var intel = I8086Decoder.get();

        var addEbIb = I8086Decoder.touchOperation(intel, [0x80, 0xC7, 0x7B]);
        var xorEbIb = I8086Decoder.touchOperation(intel, [0x80, 0x73, 0x7B, 0x0A]);
        // Some((ADD BH, Ib, 2))
        // Some((XOR [BP+DI]+0x7B, Ib, 3))
        Console.WriteLine(addEbIb);
        Console.WriteLine(xorEbIb); 
    }
    [TestMethod]
    public void TestI8086Group2()
    {
        var intel = I8086Decoder.get();

        var shl = I8086Decoder.touchOperation(intel, [0xD2, 0x24]);
        var ror = I8086Decoder.touchOperation(intel, [0xD1, 0xCB]);
        // Some((SHL [SI], CL, 2))
        // Some((ROR BX, 1, 2))
        Console.WriteLine(shl);
        Console.WriteLine(ror);
    }
    
    [TestMethod]
    public void TestI8086Group3()
    {
        var intel = I8086Decoder.get();

        var div = I8086Decoder.touchOperation(intel, [0xF7, 0x36, 0x78, 0x56]);
        var mul = I8086Decoder.touchOperation(intel, [0xF6, 0x24]);
        // Some((DIV [BP], 2))
        // Some((MUL [SI], 2))
        Console.WriteLine(div);
        Console.WriteLine(mul);
    }
    [TestMethod]
    public void TestI8086Group4()
    {
        // reg=5, mod=00 rm=101
        // Expecting: far jump Mp uses reg=5, rm=101 ->
        // [DI] if mod=00, but direct address expected, I'd need mod=00 rm=110.
        // Actually, far jump Ep (indirect) uses reg=5, but the ModR/M byte points to memory containing the far pointer.
        // For mod=00 rm=110, it's direct address. 
        var intel = I8086Decoder.get();

        var shl = I8086Decoder.touchOperation(intel, [0xD2, 0x24]);
        var ror = I8086Decoder.touchOperation(intel, [0xD1, 0xCB]);
        // Some((SHL [SI], CL, 2))
        // Some((ROR BX, 1, 2))
        Console.WriteLine(shl);
        Console.WriteLine(ror);
    }

    [TestMethod]
    public void TestCode()
    {
        var target = @"D:\TEST\DOS\SUPAPLEX.EXE";
        // Compute mixed .CODE/.DATA segment of realmode executable.
        // In example, I took OS/2 Protected Mode executable object and try to compute realmode text segment 
        MzDumpManager manager = new MzDumpManager(target);
        var codeOffset = manager.Header.cs != 0
            ? manager.Header.e_cparhdr * 0x10 + manager.Header.cs * 0x10 + manager.Header.ip
            : 0;
        var bssOffset = manager.Header.ss != 0
            ? manager.Header.e_cparhdr * 0x10 + manager.Header.ss * 0x10 + manager.Header.sp
            : 0;
        // Open file again -> move to by CS:IP -> disassemble program block
        using var file = File.OpenRead(target);
        using var reader = new BinaryReader(file);
        
        file.Position = codeOffset;
        var codeSegment = reader.ReadBytes(bssOffset - codeOffset);
        
        var decoded = I8086Decoder.decode(codeSegment);
        
        Console.WriteLine(decoded);
    }
}