using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkImageBlit
{
    public VkImageSubresourceLayers srcSubresource;

    [NativeTypeName("VkOffset3D[2]")]
    public _srcOffsets_e__FixedBuffer srcOffsets;

    public VkImageSubresourceLayers dstSubresource;

    [NativeTypeName("VkOffset3D[2]")]
    public _dstOffsets_e__FixedBuffer dstOffsets;

    [InlineArray(2)]
    public partial struct _srcOffsets_e__FixedBuffer
    {
        public VkOffset3D e0;
    }

    [InlineArray(2)]
    public partial struct _dstOffsets_e__FixedBuffer
    {
        public VkOffset3D e0;
    }
}
