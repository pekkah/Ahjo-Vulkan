namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfacePresentModeKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkPresentModeKHR presentMode;
}
