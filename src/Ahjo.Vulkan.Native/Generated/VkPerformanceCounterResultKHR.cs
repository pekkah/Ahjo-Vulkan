using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkPerformanceCounterResultKHR
{
    [FieldOffset(0)]
    [NativeTypeName("int32_t")]
    public int int32;

    [FieldOffset(0)]
    [NativeTypeName("int64_t")]
    public long int64;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint uint32;

    [FieldOffset(0)]
    [NativeTypeName("uint64_t")]
    public ulong uint64;

    [FieldOffset(0)]
    public float float32;

    [FieldOffset(0)]
    public double float64;
}
