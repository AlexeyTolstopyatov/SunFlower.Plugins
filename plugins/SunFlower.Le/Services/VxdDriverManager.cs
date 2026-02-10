using SunFlower.Le.Headers;
using SunFlower.Le.Headers.Le;

namespace SunFlower.Le.Services;

public class VxdDriverManager : UnsafeManager
{
    public VxdHeader DriverHeader { get; set; }
    public VxdResources DriverResources { get; set; }
    public VxdDescriptionBlock DriverDescriptionBlock { get; set; }
    // Let me talk with you little about forgotten...
    // 
    // VxD Drivers are Windows "Virtual (device_name) Drivers"
    // They are use for emulating hardware behaviour for real-mode MS-DOS executables.
    // As you know -- traditional MS-DOS was operating system which lived in x86 real-mode
    //
    // MZ executables what requires many paragraphs was "Hungry Programs" (e.g. e_minexp=0xFFFF)
    // because it was real query to (hard/soft)ware to allocate memory minimum in 0xFFFF paragraphs
    // 
    // Windows Virtual Machine Manager (Windows 95/98/ME) loads image of operating system
    // which contains firstly DOS image and following next 32-bit private layer of Windows 9x
    //      VMM -> (image) DOS -> win.com -> ...
    // 
    // After the win.com call -- I think (my opinion) DOS freezes and "dies" for a sometime.
    // registers, contexts and memory model are changes -> 32-bit mode enables. Windows loads.
    // 
    // As Windows loads and 32-bit mode are enabled already -> Real hardware are hidden and
    // DOS clear images can't run anymore. Microsoft makes support of DOS clear programs through
    // the "Virtual Devices".
    // True DOS images of programs (MZ executables) what work in "DOS-Mode" (remember the shutdown menu choose)
    // are post signals to the hardware. Handlers of those signals (Virtual (Device_name) Drivers) are communicate
    // with virtual machine manager and loaded Windows drivers (.SYS) for those signals' implementation.
    // 
    // That's why VxD drivers are LE compiled and linked. They are contains 32-bit code 
    // what runs for Windows VMM and 16-bit code what bound 16-bit MZ executables and virtual DOS.
    // And are contains those parts of code together. But in other Object Pages (special allocations what needed to be
    // moved into OS memory while app is running.)
    /// <summary>
    /// Offset appends to current reader position. 
    /// </summary>
    /// <param name="reader">current reader instance</param>
    /// <param name="leHeaderLastBytes">Filled by linear header driver part</param>
    /// <param name="bundle">First and last entry in entrytable</param>
    /// <param name="objects"></param>
    public VxdDriverManager(BinaryReader reader, VxdHeader leHeaderLastBytes, EntryBundle bundle, long[] objects)
    {
        // Let me talk again... 
        // Virtual (device_name) Drivers was very dangerous parts of system
        // and any part of code was written wrong - could break down fragile Windows construction.
        // Traditional "e32_startobj" and "e32_eip" pointers set the entry point segment in executable.
        // Usually the VxD drivers having CS:EIP set to the 1st element in entry table.
        // If you look at the exports table (non-resident names table) you see name of entry with "_DDB" prefix or postfix
        // 
        // This entry is what I need now! Entry record in EntryTable can be anything
        // (not only function). And now entry record with "_DDB" are naked packed unsafe C structure
        // that holds true CS:EIP of virtual driver and other important details.
        var ddbObject = bundle.ObjectNumber;

        if (bundle.Entries[0] is Entry32Bit ddbPosition)
        {
            reader.BaseStream.Position = /*remember the FAR pointer*/ objects[ddbObject - 1] + ddbPosition.Offset;
            DriverDescriptionBlock = Fill<VxdDescriptionBlock>(reader);
        }
            
        // .386 Windows Virtual Drivers (meant Windows 3x...) don't have
        // resources. This structure usually not empty in .vxd files.
        if (DriverHeader.e32_winresoff == 0)
        {
            return;
        }
        reader.BaseStream.Position = DriverHeader.e32_winresoff;
        DriverResources = Fill<VxdResources>(reader);
    }
}