namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageRobustnessFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint robustImageAccess;
}
