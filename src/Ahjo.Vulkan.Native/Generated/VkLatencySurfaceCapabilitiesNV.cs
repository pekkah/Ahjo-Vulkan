namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLatencySurfaceCapabilitiesNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint presentModeCount;

    public VkPresentModeKHR* pPresentModes;
}
