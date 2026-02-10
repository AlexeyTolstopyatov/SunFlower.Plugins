using ObjectPage = SunFlower.Le.Models.Le.ObjectPage;

namespace SunFlower.Le.Services;

public class LePagesManager
{
    public List<ObjectPage> Pages { get; set; } = [];

    public LePagesManager(BinaryReader reader, uint offset, uint count)
    {
        reader.BaseStream.Position = offset;
        
        for (var i = 0; i < count; i++)
        {
            var entry = new Headers.Le.ObjectPage
            {
                PageIndex = reader.ReadBytes(3),
                Flags = reader.ReadByte()
            }; // суммарно это ровно 32 бита. Есть ли смысл мне выравниваться?
            //reader.ReadUInt32(); 
            
            ToModel(entry);
        }
    }

    private void ToModel(Headers.Le.ObjectPage page)
    {
        List<string> flags = [];
        
        switch (page.Flags & (byte)Headers.Le.ObjectPage.PageFlags.TypeMask)
        {
            case (byte)Headers.Le.ObjectPage.PageFlags.Legal:
                flags.Add("LEGAL");
                break;
            case (byte)Headers.Le.ObjectPage.PageFlags.Iterated:
                flags.Add("ITERATED");
                break;
            case (byte)Headers.Le.ObjectPage.PageFlags.Invalid:
                flags.Add("INVALID");
                break;
            case (byte)Headers.Le.ObjectPage.PageFlags.ZeroFilled:
                flags.Add("BSS");
                break;
        }
    
        if ((page.Flags & (byte)Headers.Le.ObjectPage.PageFlags.LastPageInFile) != 0)
        {
            flags.Add("LAST");
        }
        
        Pages.Add(new ObjectPage(page, flags));
    }
}