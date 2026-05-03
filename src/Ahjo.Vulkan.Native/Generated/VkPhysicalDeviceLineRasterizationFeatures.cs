namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLineRasterizationFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rectangularLines;

    [NativeTypeName("VkBool32")]
    public uint bresenhamLines;

    [NativeTypeName("VkBool32")]
    public uint smoothLines;

    [NativeTypeName("VkBool32")]
    public uint stippledRectangularLines;

    [NativeTypeName("VkBool32")]
    public uint stippledBresenhamLines;

    [NativeTypeName("VkBool32")]
    public uint stippledSmoothLines;
}
