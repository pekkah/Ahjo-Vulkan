namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthClampControlFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint depthClampControl;
}
