namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBufferDeviceAddressAllocationAlignmentFeaturesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint bufferDeviceAddressAllocationAlignment;
}
