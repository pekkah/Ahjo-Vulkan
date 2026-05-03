namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDynamicRenderingLocalReadFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dynamicRenderingLocalRead;
}
