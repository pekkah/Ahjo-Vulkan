using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupPresentCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t[32]")]
    public _presentMask_e__FixedBuffer presentMask;

    [NativeTypeName("VkDeviceGroupPresentModeFlagsKHR")]
    public uint modes;

    [InlineArray(32)]
    public partial struct _presentMask_e__FixedBuffer
    {
        public uint e0;
    }
}
