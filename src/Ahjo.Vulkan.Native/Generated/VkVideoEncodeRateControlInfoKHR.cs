namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeRateControlInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEncodeRateControlFlagsKHR")]
    public uint flags;

    public VkVideoEncodeRateControlModeFlagBitsKHR rateControlMode;

    [NativeTypeName("uint32_t")]
    public uint layerCount;

    [NativeTypeName("const VkVideoEncodeRateControlLayerInfoKHR *")]
    public VkVideoEncodeRateControlLayerInfoKHR* pLayers;

    [NativeTypeName("uint32_t")]
    public uint virtualBufferSizeInMs;

    [NativeTypeName("uint32_t")]
    public uint initialVirtualBufferSizeInMs;
}
