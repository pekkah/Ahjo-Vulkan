namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineVertexInputStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineVertexInputStateCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint vertexBindingDescriptionCount;

    [NativeTypeName("const VkVertexInputBindingDescription *")]
    public VkVertexInputBindingDescription* pVertexBindingDescriptions;

    [NativeTypeName("uint32_t")]
    public uint vertexAttributeDescriptionCount;

    [NativeTypeName("const VkVertexInputAttributeDescription *")]
    public VkVertexInputAttributeDescription* pVertexAttributeDescriptions;
}
