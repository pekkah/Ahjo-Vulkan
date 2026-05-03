namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayPlaneInfo2KHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDisplayModeKHR")]
    public VkDisplayModeKHR_T* mode;

    [NativeTypeName("uint32_t")]
    public uint planeIndex;
}
