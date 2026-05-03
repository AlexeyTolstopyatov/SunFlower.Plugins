using Sunflower.Mz;

namespace LinearExecutable;
[TestClass]
public class I86DisassemblerTest
{
    
    [TestMethod]
    public void TestResult()
    {
        var seed = new I86DisassemblerSeed();
        seed.Main(@"D:\TEST\DOS\BASICA.COM");
        
        Assert.AreNotEqual(seed.Status.Results.Count, 0);
    }
}