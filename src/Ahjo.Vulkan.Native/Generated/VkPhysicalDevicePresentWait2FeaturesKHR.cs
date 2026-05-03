namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentWait2FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentWait2;
}
