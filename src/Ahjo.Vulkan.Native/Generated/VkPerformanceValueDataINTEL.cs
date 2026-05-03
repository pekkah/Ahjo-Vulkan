using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkPerformanceValueDataINTEL
{
    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint value32;

    [FieldOffset(0)]
    [NativeTypeName("uint64_t")]
    public ulong value64;

    [FieldOffset(0)]
    public float valueFloat;

    [FieldOffset(0)]
    [NativeTypeName("VkBool32")]
    public uint valueBool;

    [FieldOffset(0)]
    [NativeTypeName("const char *")]
    public sbyte* valueString;
}
