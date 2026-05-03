namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSubpassShadingFeaturesHUAWEI
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint subpassShading;
}
