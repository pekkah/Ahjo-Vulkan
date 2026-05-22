namespace Ahjo.Vulkan.Native;

public partial struct VkOffset3D
{
    public VkOffset3D(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static VkOffset3D Zero => default;
}
