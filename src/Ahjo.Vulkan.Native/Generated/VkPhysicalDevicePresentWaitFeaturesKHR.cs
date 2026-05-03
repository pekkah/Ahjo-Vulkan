namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentWaitFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentWait;
}
