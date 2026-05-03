using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkDeviceOrHostAddressConstKHR
{
    [FieldOffset(0)]
    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;

    [FieldOffset(0)]
    [NativeTypeName("const void *")]
    public void* hostAddress;
}
