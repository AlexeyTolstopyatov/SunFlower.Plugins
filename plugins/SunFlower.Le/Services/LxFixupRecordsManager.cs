using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Lx;

namespace SunFlower.Le.Services;

public class LxFixupRecordsManager
{
    private object ReadTargetData(BinaryReader reader, byte targetType, FixupRecord record)
    {
        switch (targetType)
        {
            case 0x00: // Internal reference
                var internalTarget = new FixupTargetInternal();
                
                if (record.Is16BitObjectModule)
                    internalTarget.ObjectNumber = reader.ReadUInt16();
                else
                    internalTarget.ObjectNumber = reader.ReadByte();
                
                // Target offset exists not for all types of SRC
                if ((record.Source & 0x0F) != 0x02) // Не 16-bit selector fixup
                {
                    if (record.Is32BitTarget)
                        internalTarget.TargetOffset = reader.ReadUInt32();
                    else
                        internalTarget.TargetOffset = reader.ReadUInt16();
                }
                
                return internalTarget;
                
            case 0x01: // Imported reference by ordinal
                var ordinalTarget = new FixupTargetImportedOrdinal();
                
                // Module ordinal
                if (record.Is16BitObjectModule)
                    ordinalTarget.ModuleOrdinal = reader.ReadUInt16();
                else
                    ordinalTarget.ModuleOrdinal = reader.ReadByte();
                
                // Import ordinal
                if (record.Is8BitOrdinal)
                {
                    ordinalTarget.ImportOrdinal = reader.ReadByte();
                }
                else
                {
                    if (record.Is32BitTarget)
                        ordinalTarget.ImportOrdinal = reader.ReadUInt32();
                    else
                        ordinalTarget.ImportOrdinal = reader.ReadUInt16();
                }
                
                return ordinalTarget;
                
            case 0x02: // Imported reference by name
                var nameTarget = new FixupTargetImportedName();
                
                // Module ordinal
                if (record.Is16BitObjectModule)
                    nameTarget.ModuleOrdinal = reader.ReadUInt16();
                else
                    nameTarget.ModuleOrdinal = reader.ReadByte();
                
                // Procedure name offset
                if (record.Is32BitTarget)
                    nameTarget.ProcedureNameOffset = reader.ReadUInt32();
                else
                    nameTarget.ProcedureNameOffset = reader.ReadUInt16();
                
                return nameTarget;
                
            case 0x03: // Internal reference via entry table
                var entryTarget = new FixupTargetEntryTable();
                
                if (record.Is16BitObjectModule)
                    entryTarget.EntryNumber = reader.ReadUInt16();
                else
                    entryTarget.EntryNumber = reader.ReadByte();
                
                return entryTarget;
                
            default:
                throw new InvalidDataException($"Unknown target type: {targetType}");
        }
    }
    private FixupRecord? ReadSingleFixupRecord(BinaryReader reader)
    {
        try
        {
            var record = new FixupRecord
            {
                // Base constructor
                Source = reader.ReadByte(),
                TargetFlags = reader.ReadByte()
            };

            // Flags
            record.HasSourceList = (record.Source & 0x20) != 0;
            record.HasAdditive = (record.TargetFlags & 0x04) != 0;
            record.Is32BitTarget = (record.TargetFlags & 0x10) != 0;
            record.Is32BitAdditive = (record.TargetFlags & 0x20) != 0;
            record.Is16BitObjectModule = (record.TargetFlags & 0x40) != 0;
            record.Is8BitOrdinal = (record.TargetFlags & 0x80) != 0;
        
            // Source Offset или Count
            if (record.HasSourceList)
            {
                record.SourceOffset = reader.ReadByte(); // <-- Source count instead
            }
            else
            {
                record.SourceOffset = reader.ReadUInt16(); // <-- Source Offset
            }
        
            var targetType = (byte)(record.TargetFlags & 0x03);
            record.TargetData = ReadTargetData(reader, targetType, record);
        
            // if additive flags?
            if (record.HasAdditive)
            {
                if (record.Is32BitAdditive)
                    record.AdditiveValue = reader.ReadUInt32();
                else
                    record.AdditiveValue = reader.ReadUInt16();
            }
        
            // sources list
            if (record.HasSourceList)
            {
                record.SourceOffsetList = new ushort[record.SourceOffset];
                for (var i = 0; i < record.SourceOffset; i++)
                {
                    record.SourceOffsetList[i] = reader.ReadUInt16();
                }
            }
        
            return record;
        }
        catch
        {
            return null;
        }
    }
    public List<FixupRecord> ReadFixupRecordsTable(BinaryReader reader, LxHeader header, long off)
    {
        var records = new List<FixupRecord>();

        long fixupRecordTableOffset = header.e32_frectab + off;
        reader.BaseStream.Seek(fixupRecordTableOffset, SeekOrigin.Begin);
    
        long endOffset = GetFixupRecordTableEnd(reader, ref header);
    
        while (reader.BaseStream.Position < endOffset)
        {
            var record = ReadSingleFixupRecord(reader);
            if (record != null)
                records.Add(record.Value);
        }
    
        return records;
    }
    private long GetFixupRecordTableEnd(BinaryReader reader, ref LxHeader header)
    {
        if (header.e32_impmod != 0) 
            return header.e32_impmod;
        
        return header.e32_fpagetab + header.e32_fixupsize;
    }
}