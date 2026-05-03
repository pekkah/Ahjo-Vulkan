namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVertexInputAttributeDescription2EXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint location;

    [NativeTypeName("uint32_t")]
    public uint binding;

    public VkFormat format;

    [NativeTypeName("uint32_t")]
    public uint offset;
}
