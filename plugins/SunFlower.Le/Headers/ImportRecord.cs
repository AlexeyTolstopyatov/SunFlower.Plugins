namespace SunFlower.Le.Headers;

public class ImportRecord(string dllName, string name, long? offset)
{
    public string DllName { get; set; } = dllName;
    public string Name { get; set; } = name;
    public long? Offset { get; set; } = offset;
}