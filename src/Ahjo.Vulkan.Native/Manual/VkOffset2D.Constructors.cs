namespace Ahjo.Vulkan.Native;

public partial struct VkOffset2D
{
    public VkOffset2D(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static VkOffset2D Zero => default;
}
