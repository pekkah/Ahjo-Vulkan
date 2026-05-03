namespace Ahjo.Vulkan.Native;

public partial struct VkVertexInputBindingDivisorDescription
{
    [NativeTypeName("uint32_t")]
    public uint binding;

    [NativeTypeName("uint32_t")]
    public uint divisor;
}
