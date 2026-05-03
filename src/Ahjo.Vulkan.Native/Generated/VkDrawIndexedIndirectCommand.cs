namespace Ahjo.Vulkan.Native;

public partial struct VkDrawIndexedIndirectCommand
{
    [NativeTypeName("uint32_t")]
    public uint indexCount;

    [NativeTypeName("uint32_t")]
    public uint instanceCount;

    [NativeTypeName("uint32_t")]
    public uint firstIndex;

    [NativeTypeName("int32_t")]
    public int vertexOffset;

    [NativeTypeName("uint32_t")]
    public uint firstInstance;
}
