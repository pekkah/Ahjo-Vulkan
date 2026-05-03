using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoVP9Segmentation
{
    public StdVideoVP9SegmentationFlags flags;

    [NativeTypeName("uint8_t[7]")]
    public _segmentation_tree_probs_e__FixedBuffer segmentation_tree_probs;

    [NativeTypeName("uint8_t[3]")]
    public _segmentation_pred_prob_e__FixedBuffer segmentation_pred_prob;

    [NativeTypeName("uint8_t[8]")]
    public _FeatureEnabled_e__FixedBuffer FeatureEnabled;

    [NativeTypeName("int16_t[8][4]")]
    public _FeatureData_e__FixedBuffer FeatureData;

    [InlineArray(7)]
    public partial struct _segmentation_tree_probs_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(3)]
    public partial struct _segmentation_pred_prob_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _FeatureEnabled_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8 * 4)]
    public partial struct _FeatureData_e__FixedBuffer
    {
        public short e0_0;
    }
}
