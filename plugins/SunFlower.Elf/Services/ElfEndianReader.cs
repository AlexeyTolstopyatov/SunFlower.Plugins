using System.Buffers.Binary;
using System.Text;

namespace SunFlower.Elf.Services;

///
/// CoffeeLake 2026-*
/// Endian-aware binary reader for ELF32/ELF64.
///
public class ElfEndianReader(Stream stream, bool bigEndian) : BinaryReader(stream, Encoding.ASCII, leaveOpen: true)
{
    public new ushort ReadUInt16() =>
        bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2)) : base.ReadUInt16();

    public new uint ReadUInt32() =>
        bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4)) : base.ReadUInt32();

    public new ulong ReadUInt64() =>
        bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(8)) : base.ReadUInt64();

    public new short ReadInt16() =>
        bigEndian ? BinaryPrimitives.ReadInt16BigEndian(ReadBytes(2)) : base.ReadInt16();

    public new int ReadInt32() =>
        bigEndian ? BinaryPrimitives.ReadInt32BigEndian(ReadBytes(4)) : base.ReadInt32();

    public new long ReadInt64() =>
        bigEndian ? BinaryPrimitives.ReadInt64BigEndian(ReadBytes(8)) : base.ReadInt64();

    public new byte[] ReadBytes(int count) => base.ReadBytes(count);
}