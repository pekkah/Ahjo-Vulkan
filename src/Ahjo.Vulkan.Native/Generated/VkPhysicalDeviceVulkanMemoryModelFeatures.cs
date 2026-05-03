namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkanMemoryModelFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint vulkanMemoryModel;

    [NativeTypeName("VkBool32")]
    public uint vulkanMemoryModelDeviceScope;

    [NativeTypeName("VkBool32")]
    public uint vulkanMemoryModelAvailabilityVisibilityChains;
}
