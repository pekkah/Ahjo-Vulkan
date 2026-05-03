using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeAV1PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeAV1PictureInfo *")]
    public StdVideoDecodeAV1PictureInfo* pStdPictureInfo;

    [NativeTypeName("int32_t[7]")]
    public _referenceNameSlotIndices_e__FixedBuffer referenceNameSlotIndices;

    [NativeTypeName("uint32_t")]
    public uint frameHeaderOffset;

    [NativeTypeName("uint32_t")]
    public uint tileCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pTileOffsets;

    [NativeTypeName("const uint32_t *")]
    public uint* pTileSizes;

    [InlineArray(7)]
    public partial struct _referenceNameSlotIndices_e__FixedBuffer
    {
        public int e0;
    }
}
