// CoffeeLake (C) 2026-*
//
// LE decoder: thin layer over DecoderServiceBase.
// Only LE-specific page reading and Object property access are implemented here.
// All shared logic (export map, fixup map, disassembly, analysis, rendering) lives in the base.

using SunFlower.Le.Headers;
using SunFlower.Le.Models.Le;
using Object = SunFlower.Le.Headers.Le.Object;

namespace SunFlower.Le.Services;

public partial class LeDecoderService : DecoderServiceBase
{
    private readonly LeDumpManager _dump;
    private readonly string _filePath;
    private readonly ObjectPage[] _dumpPages;

    public LeDecoderService(string filePath, LeDumpManager dump)
        : base(filePath,
              (int)dump.LeHeader.e32_startobj,
              dump.LeHeader.e32_eip,
              (int)dump.LeHeader.e32_pagesize,
              dump.ImportRecords)
    {
        _filePath = filePath;
        _dump = dump;

        DumpResidentNames = dump.ResidentNames;
        DumpObjects = dump.Objects;
        _dumpPages = dump.Pages;
        DumpEntryBundles = dump.EntryBundles;
        DumpFixupRecords = dump.FixupRecords;
        DumpFixupPageOffsets = dump.FixupPageOffsets
            .Select(fpr => fpr.Offset)
            .ToArray();
    }

    protected override uint GetObjectPageMapIndex(FileObject fileObject) => fileObject.PageMapIndex;
    protected override uint GetObjectPageMapEntries(FileObject obj) => obj.PageMapEntries;
    protected override uint GetObjectVirtualSegmentSize(FileObject obj) => obj.VirtualSegmentSize;
    protected override uint GetObjectObjectFlagsMask(FileObject obj) => obj.ObjectFlagsMask;
    protected override bool GetObjectExecute(FileObject obj) => new Object(obj).Execute;
    protected override bool GetObjectResource(FileObject obj) => new Object(obj).Resource;
    protected override string[] GetObjectFlags(FileObject obj) => new Object(obj).ObjectFlags;
    protected override string GetObjectSuggestedName(FileObject obj) => Object.GetSuggestedNameByPermissions(new Object(obj));

    protected override byte[]? BuildObjectBytes(FileObject obj) => BuildObjectBytesByPageIndex(new Object(obj));

    private byte[]? BuildObjectBytesByPageIndex(Object obj)
    {
        if (obj.PageMapEntries == 0) return null;
        var startLogical = (int)obj.PageMapIndex - 1; // !!! PageMap# - 1
        if (startLogical < 0) return null;

        var pagesToRead = Math.Min((int)obj.PageMapEntries, _dump.Pages.Length - startLogical);
        if (pagesToRead <= 0) return null;

        var totalSize = pagesToRead * PageSize;
        var buf = new byte[totalSize];

        using var fs = File.OpenRead(_filePath);
        using var reader = new BinaryReader(fs);

        for (var i = 0; i < pagesToRead; i++)
        {
            var page = _dump.Pages[startLogical + i].Page;
            var pageType = (byte)((byte)page.Flags & 0x03);
            if (pageType is not (0 or 3)) continue;

            var fileOffset = _dump.LeHeader.e32_datapage + (page.LongPageIndex - 1) * PageSize;
            var isLastPage = ((byte)page.Flags & 0x80) != 0;
            var bytesToRead = isLastPage ? (int)_dump.LeHeader.e32_lastpagesize : PageSize;
            if (bytesToRead <= 0) bytesToRead = PageSize;

            try
            {
                reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
                var pageBytes = new byte[bytesToRead];
                var read = reader.Read(pageBytes, 0, bytesToRead);
                var destOff = i * PageSize;
                var copyLen = Math.Min(read, totalSize - destOff);
                if (copyLen > 0) Array.Copy(pageBytes, 0, buf, destOff, copyLen);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{nameof(BuildObjectBytesByPageIndex)}]: {e}");
            }
        }

        return buf;
    }
}


