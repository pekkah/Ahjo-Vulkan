namespace Ahjo.Vulkan.Native;

public partial struct VkExtent3D
{
    public VkExtent3D(uint width, uint height, uint depth)
    {
        this.width  = width;
        this.height = height;
        this.depth  = depth;
    }
}
