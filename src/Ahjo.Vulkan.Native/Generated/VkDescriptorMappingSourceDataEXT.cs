using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkDescriptorMappingSourceDataEXT
{
    [FieldOffset(0)]
    public VkDescriptorMappingSourceConstantOffsetEXT constantOffset;

    [FieldOffset(0)]
    public VkDescriptorMappingSourcePushIndexEXT pushIndex;

    [FieldOffset(0)]
    public VkDescriptorMappingSourceIndirectIndexEXT indirectIndex;

    [FieldOffset(0)]
    public VkDescriptorMappingSourceIndirectIndexArrayEXT indirectIndexArray;

    [FieldOffset(0)]
    public VkDescriptorMappingSourceHeapDataEXT heapData;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint pushDataOffset;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint pushAddressOffset;

    [FieldOffset(0)]
    public VkDescriptorMappingSourceIndirectAddressEXT indirectAddress;

    [FieldOffset(0)]
    public VkDescriptorMappingSourceShaderRecordIndexEXT shaderRecordIndex;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint shaderRecordDataOffset;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t")]
    public uint shaderRecordAddressOffset;
}
