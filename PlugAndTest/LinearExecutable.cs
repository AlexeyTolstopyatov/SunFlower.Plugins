using SunFlower.Le.Services;

namespace LinearExecutable;

[TestClass]
public class LinearExecutable
{
    [TestMethod]
    public void ImportsStreamBounds()
    {
        LeDumpManager manager = new LeDumpManager(@"D:\TEST\MS_OS220\BMSCALLS.DLL");
        
        Console.WriteLine(manager.GetPhysicalOffset((int)manager.LeHeader.e32_stackobj, manager.LeHeader.e32_eip));
        Console.WriteLine(manager.ImportRecords.Count);
    }

    [TestMethod]
    public void PageOffset()
    {
        LeDumpManager manager = new(@"D:\TEST\MS_OS220\BMSCALLS.DLL");
        
        var startOffset = manager.GetPhysicalOffset((int)manager.LeHeader.e32_startobj, 0);
        Console.WriteLine(startOffset);
        Assert.IsTrue(startOffset % 16 == 0); // Paragraph alignment
    }
    [TestMethod]
    public void ImportsTest()
    {
        LxDumpManager manager = new(@"D:\TEST\ECS\OS2CHAR.DLL");

        manager.Offset(1);
    }
}