namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthClampZeroOneFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint depthClampZeroOne;
}
