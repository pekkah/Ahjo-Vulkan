namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVertexInputBindingDescription2EXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint binding;

    [NativeTypeName("uint32_t")]
    public uint stride;

    public VkVertexInputRate inputRate;

    [NativeTypeName("uint32_t")]
    public uint divisor;
}
