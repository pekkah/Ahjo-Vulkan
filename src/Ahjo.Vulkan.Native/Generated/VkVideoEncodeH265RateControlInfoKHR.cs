namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265RateControlInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEncodeH265RateControlFlagsKHR")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint gopFrameCount;

    [NativeTypeName("uint32_t")]
    public uint idrPeriod;

    [NativeTypeName("uint32_t")]
    public uint consecutiveBFrameCount;

    [NativeTypeName("uint32_t")]
    public uint subLayerCount;
}
