namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentId2FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentId2;
}
