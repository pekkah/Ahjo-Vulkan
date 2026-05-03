namespace Ahjo.Vulkan.Native;

public partial struct VkMultiDrawIndexedInfoEXT
{
    [NativeTypeName("uint32_t")]
    public uint firstIndex;

    [NativeTypeName("uint32_t")]
    public uint indexCount;

    [NativeTypeName("int32_t")]
    public int vertexOffset;
}
