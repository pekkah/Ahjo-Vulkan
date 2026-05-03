namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthClipControlFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint depthClipControl;
}
