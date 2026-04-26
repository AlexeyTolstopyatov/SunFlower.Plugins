using SunFlower.Le.Services;

namespace LinearExecutable;

[TestClass]
public class LinearExecutable
{
    [TestMethod]
    public void ImportsStreamBounds()
    {
        LeDumpManager manager = new LeDumpManager(@"D:\TEST\MS_OS220\BMSCALLS.DLL");
        
        Console.WriteLine(manager.ImportRecords.Count);
    }
}