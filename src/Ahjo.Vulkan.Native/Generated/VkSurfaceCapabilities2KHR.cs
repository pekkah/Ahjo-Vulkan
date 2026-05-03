namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfaceCapabilities2KHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkSurfaceCapabilitiesKHR surfaceCapabilities;
}
