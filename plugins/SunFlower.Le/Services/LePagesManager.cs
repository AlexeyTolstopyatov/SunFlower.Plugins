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
                Flags = (Headers.Le.ObjectPage.PageFlags)reader.ReadByte()
            }; 
            // Exactly 32 bits. No more alignments 
            // reader.ReadUInt32(); 
            
            ToModel(entry);
        }
    }

    private void ToModel(Headers.Le.ObjectPage page)
    {
        List<string> flags = [];
        
        Pages.Add(new ObjectPage(page, flags));
    }
}