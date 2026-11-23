using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class FixupRecordsManager
{
    /// <summary>
    /// Reads target data based on the relocation type from the LE fixup record.
    /// </summary>
    private static object ReadTargetData(BinaryReader reader, SourceType sourceType, RelocationFlags flags)
    {
        switch (flags.RelocationType)
        {
            case FixupRelocationType.Internal:
            {
                ushort objectNumber;
                if (flags.Is16BitObjectModule)
                    objectNumber = reader.ReadUInt16();
                else
                    objectNumber = reader.ReadByte();

                uint targetOffset = 0;
                if (sourceType.AddressType != FixupAddressType.Selector16)
                {
                    if (flags.Is32BitTargetOffset)
                    {
                        targetOffset = reader.ReadUInt32();
#if DEBUG
                        FixupDebug.Log($"  [Internal] targetOff read as 32-bit: 0x{targetOffset:X8}");
#endif
                    }
                    else
                    {
                        targetOffset = reader.ReadUInt16();
#if DEBUG
                        FixupDebug.Log($"  [Internal] targetOff read as 16-bit: 0x{targetOffset:X4}");
#endif
                    }
                }
#if DEBUG
                FixupDebug.Log($"  [Internal] obj={objectNumber}, off=0x{targetOffset:X4}, " +
                               $"pos=0x{reader.BaseStream.Position:X8}");
#endif
                return new FixupTargetInternal(objectNumber, targetOffset);
            }

            case FixupRelocationType.ImportOrdinal:
            {
                ushort moduleIndex;

                if (flags.Is16BitObjectModule)
                    moduleIndex = reader.ReadUInt16();
                else
                    moduleIndex = reader.ReadByte();

                uint importOrdinal;

                if (flags.Is8BitImportOrdinal)
                    importOrdinal = reader.ReadByte();
                else if (flags.Is32BitTargetOffset)
                    importOrdinal = reader.ReadUInt32();
                else
                    importOrdinal = reader.ReadUInt16();

                return new FixupTargetImportOrdinal(moduleIndex, importOrdinal);
            }

            case FixupRelocationType.ImportName:
            {
                ushort moduleIndex;
                if (flags.Is16BitObjectModule)
                    moduleIndex = reader.ReadUInt16();
                else
                    moduleIndex = reader.ReadByte();

                uint nameOffset;
                if (flags.Is32BitTargetOffset)
                    nameOffset = reader.ReadUInt32();
                else
                    nameOffset = reader.ReadUInt16();

                return new FixupTargetImportName(moduleIndex, nameOffset);
            }
            case FixupRelocationType.ViaEntryTable:
            {
                // The most painful part being honest... All relocation table are ruins if something here
                // is not compatible. OS/2 programs supports a chain fixup records and this might corrupt all
                // following definitions.
                //
                // But following the official document (1996.10.09) the construct of EntryTable Fixup is correct.
#if DEBUG
                FixupDebug.Log($"  [ViaEntryTable] posBeforeEntry=0x{reader.BaseStream.Position:X8}");
#endif
                byte[] entryData = flags.Is16BitObjectModule
                    ? reader.ReadBytes(2)
                    : reader.ReadBytes(1);
#if DEBUG
                FixupDebug.Log($"  [ViaEntryTable] entryData=[{string.Join(",", entryData.Select(b => $"0x{b:X2}"))}], " +
                               $"posAfterEntry=0x{reader.BaseStream.Position:X8}");
#endif
                return new FixupTargetEntryTable(entryData);
            }
            case FixupRelocationType.InternalChaining:
            {
                ushort objectNumber;
                if (flags.Is16BitObjectModule)
                    objectNumber = reader.ReadUInt16();
                else
                    objectNumber = reader.ReadByte();

                uint targetOffset = 0;

                if (sourceType.AddressType != FixupAddressType.Selector16)
                {
                    if (flags.Is32BitTargetOffset)
                        targetOffset = reader.ReadUInt32();
                    else
                        targetOffset = reader.ReadUInt16();
                }

                return new FixupTargetInternal(objectNumber, targetOffset);
            }
            default:
                throw new InvalidDataException($"Unknown relocation type: {flags.RelocationType}");
        }
    }

    /// <summary>
    /// Reads a single fixup record from the current stream position.
    /// </summary>
    private static FixupRecord? ReadSingleFixupRecord(BinaryReader reader)
    {
        var posBefore = reader.BaseStream.Position;
        try
        {
            var atp = reader.ReadByte();
            var rtp = reader.ReadByte();

            var sourceType = new SourceType(atp);
            var flags = new RelocationFlags(rtp);

#if DEBUG
            FixupDebug.Log(
                $"[Fixup @ 0x{posBefore:X8}] ATP=0x{atp:X2} RTP=0x{rtp:X2} | " +
                $"addr={sourceType.AddressType} reloc={flags.RelocationType} " +
                $"hasSrcList={sourceType.HasSourceList} is16BitObj={flags.Is16BitObjectModule} " +
                $"hasAdd={flags.HasAdditive} add32={flags.Is32BitAdditive} " +
                $"tgt32={flags.Is32BitTargetOffset} chain={flags.IsInternalChaining}");
#endif

            ushort sourceOffset;
            if (sourceType.HasSourceList)
                sourceOffset = reader.ReadByte();
            else
                sourceOffset = reader.ReadUInt16();

#if DEBUG
            FixupDebug.Log($"  sourceOffset=0x{sourceOffset:X4}, pos=0x{reader.BaseStream.Position:X8}");
#endif

            var targetData = ReadTargetData(reader, sourceType, flags);

#if DEBUG
            FixupDebug.Log($"  target={DescribeTarget(targetData)}, pos=0x{reader.BaseStream.Position:X8}");
#endif

            uint? additiveValue = null;
            if (flags.HasAdditive)
            {
                additiveValue = flags.Is32BitAdditive ? reader.ReadUInt32() : reader.ReadUInt16();
#if DEBUG
                FixupDebug.Log($"  additive=0x{additiveValue:X}, pos=0x{reader.BaseStream.Position:X8}");
#endif
            }

            ushort[] sourceOffsetList = [];
            if (sourceType.HasSourceList)
            {
                var list = new ushort[sourceOffset];
                for (var i = 0; i < sourceOffset; i++)
                    list[i] = reader.ReadUInt16();
                sourceOffsetList = list;
#if DEBUG
                FixupDebug.Log($"  srcOffsetList=[{string.Join(",", list.Select(x => $"0x{x:X4}"))}], " +
                               $"pos=0x{reader.BaseStream.Position:X8}");
#endif
            }

#if DEBUG
            FixupDebug.Log($"  end @ 0x{reader.BaseStream.Position:X8} " +
                           $"(record size = {reader.BaseStream.Position - posBefore})");
#endif

            return new FixupRecord(
                sourceType, flags, sourceOffset, targetData, additiveValue, sourceOffsetList);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not EndOfStreamException)
        {
            return null;
        }
    }

#if DEBUG
    private static string DescribeTarget(object target) => target switch
    {
        FixupTargetInternal i => $"Internal(obj={i.ObjectNumber}, off=0x{i.TargetOffset:X4})",
        FixupTargetImportOrdinal o => $"ImpOrd(mod={o.ModuleIndex}, ord={o.ImportOrdinal})",
        FixupTargetImportName n => $"ImpName(mod={n.ModuleIndex}, nameOff=0x{n.NameOffset:X4})",
        FixupTargetEntryTable e => $"EntryTable(ord={e.Ordinal})",
        _ => target.GetType().Name
    };
#endif

    /// <summary>
    /// Reads all fixup records from the fixup record table, organized by pages.
    /// </summary>
    public List<FixupRecord> ReadFixupRecordsTable(
        BinaryReader reader,
        uint fixupRecordTableOffset,
        List<FixupPageRecord> pageOffsets)
    {
        var records = new List<FixupRecord>();

        for (var i = 0; i < pageOffsets.Count - 1; i++)
        {
            var pageDataOffset = pageOffsets[i].Offset;
            var nextPageDataOffset = pageOffsets[i + 1].Offset;
            var recordOffset = fixupRecordTableOffset + pageDataOffset;
            var pageEndOffset = fixupRecordTableOffset + nextPageDataOffset;

#if DEBUG
            FixupDebug.Log($"[Page {i}] records: 0x{recordOffset:X8}..0x{pageEndOffset:X8} " +
                           $"({(int)(pageEndOffset - recordOffset)} bytes)");
            FixupDebug.DumpHex(reader, recordOffset,
                (int)(pageEndOffset - recordOffset), $"[Page {i} raw]");
#endif

            reader.BaseStream.Seek(recordOffset, SeekOrigin.Begin);

#if DEBUG
            var count = 0;
#endif
            while (reader.BaseStream.Position < pageEndOffset)
            {
                var record = ReadSingleFixupRecord(reader);
                if (record.HasValue)
                {
                    records.Add(new FixupRecord(
                        record.Value.SourceType,
                        record.Value.RelocationFlags,
                        record.Value.SourceOffset,
                        record.Value.TargetData,
                        record.Value.AdditiveValue,
                        record.Value.SourceOffsetList,
                        logicalPage: i));
#if DEBUG
                    count++;
#endif
                }
                else
                {
#if DEBUG
                    FixupDebug.Log($"[Page {i}] ! break at 0x{reader.BaseStream.Position:X8}");
#endif
                    break;
                }
            }
#if DEBUG
            FixupDebug.Log($"[Page {i}] parsed records: {count}");
#endif
        }

        return records;
    }
}