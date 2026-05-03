namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainPresentScalingCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPresentScalingFlagsKHR")]
    public uint scalingBehavior;

    [NativeTypeName("VkPresentGravityFlagsKHR")]
    public uint presentGravityX;

    [NativeTypeName("VkPresentGravityFlagsKHR")]
    public uint presentGravityY;
}
