namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRobustness2FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint robustBufferAccess2;

    [NativeTypeName("VkBool32")]
    public uint robustImageAccess2;

    [NativeTypeName("VkBool32")]
    public uint nullDescriptor;
}
