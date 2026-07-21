namespace Ahjo.Vulkan.Ktx.Native;

[NativeTypeName("unsigned int")]
public enum ktx_pack_astc_encoder_mode_e : uint
{
    KTX_PACK_ASTC_ENCODER_MODE_DEFAULT,
    KTX_PACK_ASTC_ENCODER_MODE_LDR,
    KTX_PACK_ASTC_ENCODER_MODE_HDR,
    KTX_PACK_ASTC_ENCODER_MODE_MAX = KTX_PACK_ASTC_ENCODER_MODE_HDR,
}
