namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorPoolCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorPoolCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint maxSets;

    [NativeTypeName("uint32_t")]
    public uint poolSizeCount;

    [NativeTypeName("const VkDescriptorPoolSize *")]
    public VkDescriptorPoolSize* pPoolSizes;
}
