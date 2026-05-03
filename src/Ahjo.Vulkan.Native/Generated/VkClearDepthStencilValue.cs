namespace Ahjo.Vulkan.Native;

public partial struct VkClearDepthStencilValue
{
    public float depth;

    [NativeTypeName("uint32_t")]
    public uint stencil;
}
