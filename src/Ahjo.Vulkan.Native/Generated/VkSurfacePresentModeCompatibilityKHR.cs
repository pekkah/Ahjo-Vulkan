namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfacePresentModeCompatibilityKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint presentModeCount;

    public VkPresentModeKHR* pPresentModes;
}
