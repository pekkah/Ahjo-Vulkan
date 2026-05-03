namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplaySurfaceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDisplaySurfaceCreateFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkDisplayModeKHR")]
    public VkDisplayModeKHR_T* displayMode;

    [NativeTypeName("uint32_t")]
    public uint planeIndex;

    [NativeTypeName("uint32_t")]
    public uint planeStackIndex;

    public VkSurfaceTransformFlagBitsKHR transform;

    public float globalAlpha;

    public VkDisplayPlaneAlphaFlagBitsKHR alphaMode;

    public VkExtent2D imageExtent;
}
