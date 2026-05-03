namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePageableDeviceLocalMemoryFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pageableDeviceLocalMemory;
}
