namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDynamicRenderingFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dynamicRendering;
}
