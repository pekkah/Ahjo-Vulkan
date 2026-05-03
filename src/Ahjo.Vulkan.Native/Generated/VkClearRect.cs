namespace Ahjo.Vulkan.Native;

public partial struct VkClearRect
{
    public VkRect2D rect;

    [NativeTypeName("uint32_t")]
    public uint baseArrayLayer;

    [NativeTypeName("uint32_t")]
    public uint layerCount;
}
