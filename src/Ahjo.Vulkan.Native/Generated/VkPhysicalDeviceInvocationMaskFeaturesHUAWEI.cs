namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceInvocationMaskFeaturesHUAWEI
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint invocationMask;
}
