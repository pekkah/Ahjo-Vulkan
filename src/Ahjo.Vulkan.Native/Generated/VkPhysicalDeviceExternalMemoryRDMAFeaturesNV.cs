namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalMemoryRDMAFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint externalMemoryRDMA;
}
