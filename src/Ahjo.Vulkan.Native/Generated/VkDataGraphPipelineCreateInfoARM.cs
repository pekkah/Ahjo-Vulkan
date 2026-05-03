namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCreateFlags2KHR")]
    public ulong flags;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("uint32_t")]
    public uint resourceInfoCount;

    [NativeTypeName("const VkDataGraphPipelineResourceInfoARM *")]
    public VkDataGraphPipelineResourceInfoARM* pResourceInfos;
}
