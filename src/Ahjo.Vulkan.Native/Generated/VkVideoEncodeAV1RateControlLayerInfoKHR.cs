namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1RateControlLayerInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMinQIndex;

    public VkVideoEncodeAV1QIndexKHR minQIndex;

    [NativeTypeName("VkBool32")]
    public uint useMaxQIndex;

    public VkVideoEncodeAV1QIndexKHR maxQIndex;

    [NativeTypeName("VkBool32")]
    public uint useMaxFrameSize;

    public VkVideoEncodeAV1FrameSizeKHR maxFrameSize;
}
