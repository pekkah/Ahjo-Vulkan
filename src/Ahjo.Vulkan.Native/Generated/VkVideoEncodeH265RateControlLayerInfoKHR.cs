namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265RateControlLayerInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMinQp;

    public VkVideoEncodeH265QpKHR minQp;

    [NativeTypeName("VkBool32")]
    public uint useMaxQp;

    public VkVideoEncodeH265QpKHR maxQp;

    [NativeTypeName("VkBool32")]
    public uint useMaxFrameSize;

    public VkVideoEncodeH265FrameSizeKHR maxFrameSize;
}
