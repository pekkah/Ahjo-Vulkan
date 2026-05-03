namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFormatPackFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint formatPack;
}
