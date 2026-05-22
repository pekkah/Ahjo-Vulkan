namespace Ahjo.Vulkan.Native;

public partial struct VkRect2D
{
    public VkRect2D(VkOffset2D offset, VkExtent2D extent)
    {
        this.offset = offset;
        this.extent = extent;
    }
}
