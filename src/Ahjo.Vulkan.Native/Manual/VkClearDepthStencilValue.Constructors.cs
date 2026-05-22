namespace Ahjo.Vulkan.Native;

public partial struct VkClearDepthStencilValue
{
    public VkClearDepthStencilValue(float depth, uint stencil)
    {
        this.depth   = depth;
        this.stencil = stencil;
    }
}
