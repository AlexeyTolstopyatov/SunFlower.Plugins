using SunFlower.Le.Headers;

namespace SunFlower.Le.Services;

public class FixupRecordsManager
{
    /// <summary>
    /// Reads target data based on the relocation type from the LE fixup record.
    /// </summary>
    private static object ReadTargetData(BinaryReader reader, LeSourceType sourceType, LeRelocationFlags flags)
    {
        switch (flags.RelocationType)
        {
            case LeFixupRelocationType.Internal:
            {
                ushort objectNumber;
                if (flags.Is16BitObjectModule)
                    objectNumber = reader.ReadUInt16();
                else
                    objectNumber = reader.ReadByte();

                uint targetOffset = 0;
                
                if (sourceType.AddressType != LeFixupAddressType.Selector16)
                {
                    if (flags.Is32BitTargetOffset)
                        targetOffset = reader.ReadUInt32();
                    else
                        targetOffset = reader.ReadUInt16();
                }

                return new LeFixupTargetInternal(objectNumber, targetOffset);
            }

            case LeFixupRelocationType.ImportOrdinal:
            {
                ushort moduleIndex;

                if (flags.Is16BitObjectModule)
                    moduleIndex = reader.ReadUInt16();
                else
                    moduleIndex = reader.ReadByte();


                uint importOrdinal;
                if (flags.Is8BitImportOrdinal)
                {
                    importOrdinal = reader.ReadByte();
                }
                else
                {
                    if (flags.Is32BitTargetOffset)
                        importOrdinal = reader.ReadUInt32();
                    else
                        importOrdinal = reader.ReadUInt16();
                }

                return new LeFixupTargetImportOrdinal(moduleIndex, importOrdinal);
            }

            case LeFixupRelocationType.ImportName:
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

                return new LeFixupTargetImportName(moduleIndex, nameOffset);
            }

            case LeFixupRelocationType.OsFixup:
            {
                var data = new byte[2];
                if (flags.Is16BitObjectModule)
                    data = reader.ReadBytes(2);
                else 
                    data = reader.ReadBytes(1);
                    
                return new LeFixupTargetEntryTable(data);
            }

            default:
                throw new InvalidDataException($"Unknown relocation type: {flags.RelocationType}");
        }
    }
    /// <summary>
    /// Reads a single LE fixup record from the current stream position.
    /// </summary>
    private static LeFixupRecord? ReadSingleFixupRecord(BinaryReader reader)
    {
        try
        {
            // Read the two header bytes
            var atp = reader.ReadByte();
            var rtp = reader.ReadByte();

            var sourceType = new LeSourceType(atp);
            var flags = new LeRelocationFlags(rtp);

            // Read source offset or count
            ushort sourceOffset;
            if (sourceType.HasSourceList)
            {
                // List mode: next byte is the count of source offsets
                sourceOffset = reader.ReadByte();
            }
            else
            {
                // Normal mode: next 2 bytes are the source offset
                sourceOffset = reader.ReadUInt16();
            }

            // Read target data
            var targetData = ReadTargetData(reader, sourceType, flags);

            // Read additive value if present
            uint? additiveValue = null;
            if (flags.HasAdditive)
            {
                additiveValue = flags.Is32BitAdditive
                    ? reader.ReadUInt32()
                    : reader.ReadUInt16();
            }

            // Read source offset list if present
            ushort[] sourceOffsetList = null;
            if (sourceType.HasSourceList)
            {
                var list = new ushort[sourceOffset];
                for (var i = 0; i < sourceOffset; i++)
                {
                    list[i] = reader.ReadUInt16();
                }
                sourceOffsetList = list;
            }

            return new LeFixupRecord(
                sourceType,
                flags,
                sourceOffset,
                targetData,
                additiveValue,
                sourceOffsetList
            );
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not EndOfStreamException)
        {
            // Log or handle unexpected errors
            return null;
        }
    }

    /// <summary>
    /// Reads all fixup records from the fixup record table, organized by pages.
    /// </summary>
    public List<LeFixupRecord> ReadFixupRecordsTable(
        BinaryReader reader,
        uint fixupRecordTableOffset,
        List<FixupPageRecord> pageOffsets)
    {
        var records = new List<LeFixupRecord>();

        for (var i = 0; i < pageOffsets.Count - 1; i++)
        {
            var pageDataOffset = pageOffsets[i].Offset;
            var nextPageDataOffset = pageOffsets[i + 1].Offset;

            // Skip empty pages (no fixups)
            if (pageDataOffset == nextPageDataOffset)
                continue;

            var recordOffset = fixupRecordTableOffset + pageDataOffset;
            reader.BaseStream.Seek(recordOffset, SeekOrigin.Begin);

            var pageEndOffset = fixupRecordTableOffset + nextPageDataOffset;

            while (reader.BaseStream.Position < pageEndOffset)
            {
                var record = ReadSingleFixupRecord(reader);
                if (record.HasValue)
                {
                    // Assign the logical page index (i) to each record
                    records.Add(new LeFixupRecord(
                        record.Value.SourceType,
                        record.Value.RelocationFlags,
                        record.Value.SourceOffset,
                        record.Value.TargetData,
                        record.Value.AdditiveValue,
                        record.Value.SourceOffsetList,
                        logicalPage: i));
                }
                else
                    break;
            }
        }

        return records;
    }
}