namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthClipEnableFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint depthClipEnable;
}
