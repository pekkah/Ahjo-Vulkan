namespace Ahjo.Vulkan.Ktx.Native;

[NativeTypeName("unsigned int")]
public enum ktx_transcode_flag_bits_e : uint
{
    KTX_TF_PVRTC_DECODE_TO_NEXT_POW2 = 2,
    KTX_TF_TRANSCODE_ALPHA_DATA_TO_OPAQUE_FORMATS = 4,
    KTX_TF_HIGH_QUALITY = 32,
}
