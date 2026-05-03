namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCommandBufferInheritanceFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint commandBufferInheritance;
}
