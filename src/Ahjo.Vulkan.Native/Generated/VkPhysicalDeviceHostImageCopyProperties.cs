using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceHostImageCopyProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint copySrcLayoutCount;

    public VkImageLayout* pCopySrcLayouts;

    [NativeTypeName("uint32_t")]
    public uint copyDstLayoutCount;

    public VkImageLayout* pCopyDstLayouts;

    [NativeTypeName("uint8_t[16]")]
    public _optimalTilingLayoutUUID_e__FixedBuffer optimalTilingLayoutUUID;

    [NativeTypeName("VkBool32")]
    public uint identicalMemoryTypeRequirements;

    [InlineArray(16)]
    public partial struct _optimalTilingLayoutUUID_e__FixedBuffer
    {
        public byte e0;
    }
}
