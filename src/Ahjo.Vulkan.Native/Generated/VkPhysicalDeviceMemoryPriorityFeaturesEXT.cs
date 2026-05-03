namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMemoryPriorityFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint memoryPriority;
}
