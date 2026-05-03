namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAttachmentDescriptionStencilLayout
{
    public VkStructureType sType;

    public void* pNext;

    public VkImageLayout stencilInitialLayout;

    public VkImageLayout stencilFinalLayout;
}
