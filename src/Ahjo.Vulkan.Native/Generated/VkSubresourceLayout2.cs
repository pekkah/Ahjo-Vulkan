namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubresourceLayout2
{
    public VkStructureType sType;

    public void* pNext;

    public VkSubresourceLayout subresourceLayout;
}
