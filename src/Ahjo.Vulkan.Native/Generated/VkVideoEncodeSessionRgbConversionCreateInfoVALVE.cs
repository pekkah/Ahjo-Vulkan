namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeSessionRgbConversionCreateInfoVALVE
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkVideoEncodeRgbModelConversionFlagBitsVALVE rgbModel;

    public VkVideoEncodeRgbRangeCompressionFlagBitsVALVE rgbRange;

    public VkVideoEncodeRgbChromaOffsetFlagBitsVALVE xChromaOffset;

    public VkVideoEncodeRgbChromaOffsetFlagBitsVALVE yChromaOffset;
}
