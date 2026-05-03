namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceUnifiedImageLayoutsFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint unifiedImageLayouts;

    [NativeTypeName("VkBool32")]
    public uint unifiedImageLayoutsVideo;
}
