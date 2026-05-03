namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageCompressionControlSwapchainFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageCompressionControlSwapchain;
}
