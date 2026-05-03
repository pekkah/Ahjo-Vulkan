using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoEncodeAV1PictureInfo
{
    public StdVideoEncodeAV1PictureInfoFlags flags;

    public StdVideoAV1FrameType frame_type;

    [NativeTypeName("uint32_t")]
    public uint frame_presentation_time;

    [NativeTypeName("uint32_t")]
    public uint current_frame_id;

    [NativeTypeName("uint8_t")]
    public byte order_hint;

    [NativeTypeName("uint8_t")]
    public byte primary_ref_frame;

    [NativeTypeName("uint8_t")]
    public byte refresh_frame_flags;

    [NativeTypeName("uint8_t")]
    public byte coded_denom;

    [NativeTypeName("uint16_t")]
    public ushort render_width_minus_1;

    [NativeTypeName("uint16_t")]
    public ushort render_height_minus_1;

    public StdVideoAV1InterpolationFilter interpolation_filter;

    public StdVideoAV1TxMode TxMode;

    [NativeTypeName("uint8_t")]
    public byte delta_q_res;

    [NativeTypeName("uint8_t")]
    public byte delta_lf_res;

    [NativeTypeName("uint8_t[8]")]
    public _ref_order_hint_e__FixedBuffer ref_order_hint;

    [NativeTypeName("int8_t[7]")]
    public _ref_frame_idx_e__FixedBuffer ref_frame_idx;

    [NativeTypeName("uint8_t[3]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("uint32_t[7]")]
    public _delta_frame_id_minus_1_e__FixedBuffer delta_frame_id_minus_1;

    [NativeTypeName("const StdVideoAV1TileInfo *")]
    public StdVideoAV1TileInfo* pTileInfo;

    [NativeTypeName("const StdVideoAV1Quantization *")]
    public StdVideoAV1Quantization* pQuantization;

    [NativeTypeName("const StdVideoAV1Segmentation *")]
    public StdVideoAV1Segmentation* pSegmentation;

    [NativeTypeName("const StdVideoAV1LoopFilter *")]
    public StdVideoAV1LoopFilter* pLoopFilter;

    [NativeTypeName("const StdVideoAV1CDEF *")]
    public StdVideoAV1CDEF* pCDEF;

    [NativeTypeName("const StdVideoAV1LoopRestoration *")]
    public StdVideoAV1LoopRestoration* pLoopRestoration;

    [NativeTypeName("const StdVideoAV1GlobalMotion *")]
    public StdVideoAV1GlobalMotion* pGlobalMotion;

    [NativeTypeName("const StdVideoEncodeAV1ExtensionHeader *")]
    public StdVideoEncodeAV1ExtensionHeader* pExtensionHeader;

    [NativeTypeName("const uint32_t *")]
    public uint* pBufferRemovalTimes;

    [InlineArray(8)]
    public partial struct _ref_order_hint_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(7)]
    public partial struct _ref_frame_idx_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(3)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(7)]
    public partial struct _delta_frame_id_minus_1_e__FixedBuffer
    {
        public uint e0;
    }
}
