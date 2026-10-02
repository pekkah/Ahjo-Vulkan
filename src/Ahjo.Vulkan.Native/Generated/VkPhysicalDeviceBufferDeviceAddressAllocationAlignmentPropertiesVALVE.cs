namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBufferDeviceAddressAllocationAlignmentPropertiesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxBufferDeviceAddressAllocationAlignment;
}
