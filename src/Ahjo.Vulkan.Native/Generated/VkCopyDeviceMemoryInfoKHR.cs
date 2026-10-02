namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyDeviceMemoryInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkDeviceMemoryCopyKHR *")]
    public VkDeviceMemoryCopyKHR* pRegions;
}
