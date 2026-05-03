namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264RateControlLayerInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMinQp;

    public VkVideoEncodeH264QpKHR minQp;

    [NativeTypeName("VkBool32")]
    public uint useMaxQp;

    public VkVideoEncodeH264QpKHR maxQp;

    [NativeTypeName("VkBool32")]
    public uint useMaxFrameSize;

    public VkVideoEncodeH264FrameSizeKHR maxFrameSize;
}
