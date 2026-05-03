namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTransformFeedbackPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxTransformFeedbackStreams;

    [NativeTypeName("uint32_t")]
    public uint maxTransformFeedbackBuffers;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxTransformFeedbackBufferSize;

    [NativeTypeName("uint32_t")]
    public uint maxTransformFeedbackStreamDataSize;

    [NativeTypeName("uint32_t")]
    public uint maxTransformFeedbackBufferDataSize;

    [NativeTypeName("uint32_t")]
    public uint maxTransformFeedbackBufferDataStride;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackQueries;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackStreamsLinesTriangles;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackRasterizationStreamSelect;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackDraw;
}
