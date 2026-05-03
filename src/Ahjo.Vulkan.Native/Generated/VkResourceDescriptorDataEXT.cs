using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkResourceDescriptorDataEXT
{
    [FieldOffset(0)]
    [NativeTypeName("const VkImageDescriptorInfoEXT *")]
    public VkImageDescriptorInfoEXT* pImage;

    [FieldOffset(0)]
    [NativeTypeName("const VkTexelBufferDescriptorInfoEXT *")]
    public VkTexelBufferDescriptorInfoEXT* pTexelBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkDeviceAddressRangeEXT *")]
    public VkDeviceAddressRangeEXT* pAddressRange;

    [FieldOffset(0)]
    [NativeTypeName("const VkTensorViewCreateInfoARM *")]
    public VkTensorViewCreateInfoARM* pTensorARM;
}
