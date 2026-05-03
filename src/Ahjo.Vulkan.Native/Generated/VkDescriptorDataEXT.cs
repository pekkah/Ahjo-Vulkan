using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkDescriptorDataEXT
{
    [FieldOffset(0)]
    [NativeTypeName("const VkSampler *")]
    public VkSampler_T** pSampler;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorImageInfo *")]
    public VkDescriptorImageInfo* pCombinedImageSampler;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorImageInfo *")]
    public VkDescriptorImageInfo* pInputAttachmentImage;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorImageInfo *")]
    public VkDescriptorImageInfo* pSampledImage;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorImageInfo *")]
    public VkDescriptorImageInfo* pStorageImage;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorAddressInfoEXT *")]
    public VkDescriptorAddressInfoEXT* pUniformTexelBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorAddressInfoEXT *")]
    public VkDescriptorAddressInfoEXT* pStorageTexelBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorAddressInfoEXT *")]
    public VkDescriptorAddressInfoEXT* pUniformBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkDescriptorAddressInfoEXT *")]
    public VkDescriptorAddressInfoEXT* pStorageBuffer;

    [FieldOffset(0)]
    [NativeTypeName("VkDeviceAddress")]
    public ulong accelerationStructure;
}
