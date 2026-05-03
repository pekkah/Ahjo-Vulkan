namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPushConstantsInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stageFlags;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("const void *")]
    public void* pValues;
}
