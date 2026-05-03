using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeVP9PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeVP9PictureInfo *")]
    public StdVideoDecodeVP9PictureInfo* pStdPictureInfo;

    [NativeTypeName("int32_t[3]")]
    public _referenceNameSlotIndices_e__FixedBuffer referenceNameSlotIndices;

    [NativeTypeName("uint32_t")]
    public uint uncompressedHeaderOffset;

    [NativeTypeName("uint32_t")]
    public uint compressedHeaderOffset;

    [NativeTypeName("uint32_t")]
    public uint tilesOffset;

    [InlineArray(3)]
    public partial struct _referenceNameSlotIndices_e__FixedBuffer
    {
        public int e0;
    }
}
