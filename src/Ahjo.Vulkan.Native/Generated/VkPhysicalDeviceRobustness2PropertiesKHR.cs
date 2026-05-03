namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRobustness2PropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong robustStorageBufferAccessSizeAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong robustUniformBufferAccessSizeAlignment;
}
