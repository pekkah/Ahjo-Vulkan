namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceProtectedMemoryFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint protectedMemory;
}
