namespace Ahjo.Vulkan.Native;

public partial struct VkViewport
{
    public VkViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        this.x        = x;
        this.y        = y;
        this.width    = width;
        this.height   = height;
        this.minDepth = minDepth;
        this.maxDepth = maxDepth;
    }
}
