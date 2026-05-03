namespace Ahjo.Vulkan.Native;

public partial struct VkDrawIndirectCommand
{
    [NativeTypeName("uint32_t")]
    public uint vertexCount;

    [NativeTypeName("uint32_t")]
    public uint instanceCount;

    [NativeTypeName("uint32_t")]
    public uint firstVertex;

    [NativeTypeName("uint32_t")]
    public uint firstInstance;
}
