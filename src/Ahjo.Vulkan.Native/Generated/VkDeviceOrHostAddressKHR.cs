using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkDeviceOrHostAddressKHR
{
    [FieldOffset(0)]
    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;

    [FieldOffset(0)]
    public void* hostAddress;
}
