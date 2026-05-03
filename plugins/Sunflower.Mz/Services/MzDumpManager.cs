using Sunflower.Mz.Models;

namespace Sunflower.Mz.Services;

public class MzDumpManager
{
    public MzHeader Header { get; init; }
    public List<MzRelocation> Relocations { get; init; }
    public long CodeOffset { get; init; }
    public long StackOffset { get; init; }
    public long CodePointer { get; init; }
    public long StackPointer { get; init; }
    public MzDumpManager(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(stream);

        var mzModel = new MzHeaderModel(reader); // all done
        
        Header = mzModel.Header;
        Relocations = mzModel.Relocations;
        CodeOffset = mzModel.CodeOffset;
        StackOffset = mzModel.StackOffset;
        CodePointer = mzModel.CodePointer;
        StackPointer = mzModel.StackPointer;
        
        reader.Close();
    }
}