namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineColorWriteCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkBool32 *")]
    public uint* pColorWriteEnables;
}
