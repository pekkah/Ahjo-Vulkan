namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoEncodeIntraRefreshFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoEncodeIntraRefresh;
}
