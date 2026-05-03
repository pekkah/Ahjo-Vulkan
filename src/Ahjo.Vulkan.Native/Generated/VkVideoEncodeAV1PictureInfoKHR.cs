using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkVideoEncodeAV1PredictionModeKHR predictionMode;

    public VkVideoEncodeAV1RateControlGroupKHR rateControlGroup;

    [NativeTypeName("uint32_t")]
    public uint constantQIndex;

    [NativeTypeName("const StdVideoEncodeAV1PictureInfo *")]
    public StdVideoEncodeAV1PictureInfo* pStdPictureInfo;

    [NativeTypeName("int32_t[7]")]
    public _referenceNameSlotIndices_e__FixedBuffer referenceNameSlotIndices;

    [NativeTypeName("VkBool32")]
    public uint primaryReferenceCdfOnly;

    [NativeTypeName("VkBool32")]
    public uint generateObuExtensionHeader;

    [InlineArray(7)]
    public partial struct _referenceNameSlotIndices_e__FixedBuffer
    {
        public int e0;
    }
}
