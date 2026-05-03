namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeRgbConversionCapabilitiesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeRgbModelConversionFlagsVALVE")]
    public uint rgbModels;

    [NativeTypeName("VkVideoEncodeRgbRangeCompressionFlagsVALVE")]
    public uint rgbRanges;

    [NativeTypeName("VkVideoEncodeRgbChromaOffsetFlagsVALVE")]
    public uint xChromaOffsets;

    [NativeTypeName("VkVideoEncodeRgbChromaOffsetFlagsVALVE")]
    public uint yChromaOffsets;
}
