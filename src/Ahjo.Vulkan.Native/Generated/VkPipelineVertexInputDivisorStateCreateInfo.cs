namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineVertexInputDivisorStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint vertexBindingDivisorCount;

    [NativeTypeName("const VkVertexInputBindingDivisorDescription *")]
    public VkVertexInputBindingDivisorDescription* pVertexBindingDivisors;
}
