namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCustomResolveFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint customResolve;
}
