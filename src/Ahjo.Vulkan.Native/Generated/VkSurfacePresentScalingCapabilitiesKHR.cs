namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfacePresentScalingCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPresentScalingFlagsKHR")]
    public uint supportedPresentScaling;

    [NativeTypeName("VkPresentGravityFlagsKHR")]
    public uint supportedPresentGravityX;

    [NativeTypeName("VkPresentGravityFlagsKHR")]
    public uint supportedPresentGravityY;

    public VkExtent2D minScaledImageExtent;

    public VkExtent2D maxScaledImageExtent;
}
