using System.Runtime.InteropServices;
using Sunflower.Links.Flags;

namespace Sunflower.Links.Headers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Windows3x286
{
    public ushort XmsMemMaxSizeK;
    public ushort XmsMemReqSizeK;
    public Win286KeyboardFlags Flags;
}