namespace SunFlower.Le.Models.Le;

public class ObjectPage(Headers.Le.ObjectPage page, List<string> flags)
{
    public Headers.Le.ObjectPage Page { get; set; } = page;
    public string[] Flags { get; set; } = flags.ToArray();
}