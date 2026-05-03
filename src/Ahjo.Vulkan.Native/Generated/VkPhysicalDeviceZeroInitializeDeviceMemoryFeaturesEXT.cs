namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceZeroInitializeDeviceMemoryFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint zeroInitializeDeviceMemory;
}
