namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfaceFormat2KHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkSurfaceFormatKHR surfaceFormat;
}
