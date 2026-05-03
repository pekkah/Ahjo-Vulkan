using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkPipelineExecutableStatisticValueKHR
{
    [FieldOffset(0)]
    [NativeTypeName("VkBool32")]
    public uint b32;

    [FieldOffset(0)]
    [NativeTypeName("int64_t")]
    public long i64;

    [FieldOffset(0)]
    [NativeTypeName("uint64_t")]
    public ulong u64;

    [FieldOffset(0)]
    public double f64;
}
