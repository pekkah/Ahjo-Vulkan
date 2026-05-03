namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentIdFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentId;
}
