namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;

    [NativeTypeName("VkIndirectCommandsLayoutNV")]
    public VkIndirectCommandsLayoutNV_T* indirectCommandsLayout;

    [NativeTypeName("uint32_t")]
    public uint streamCount;

    [NativeTypeName("const VkIndirectCommandsStreamNV *")]
    public VkIndirectCommandsStreamNV* pStreams;

    [NativeTypeName("uint32_t")]
    public uint sequencesCount;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* preprocessBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong preprocessOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong preprocessSize;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* sequencesCountBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong sequencesCountOffset;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* sequencesIndexBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong sequencesIndexOffset;
}
