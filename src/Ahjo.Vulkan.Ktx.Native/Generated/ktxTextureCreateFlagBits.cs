namespace Ahjo.Vulkan.Ktx.Native;

[NativeTypeName("unsigned int")]
public enum ktxTextureCreateFlagBits : uint
{
    KTX_TEXTURE_CREATE_NO_FLAGS = 0x00,
    KTX_TEXTURE_CREATE_LOAD_IMAGE_DATA_BIT = 0x01,
    KTX_TEXTURE_CREATE_RAW_KVDATA_BIT = 0x02,
    KTX_TEXTURE_CREATE_SKIP_KVDATA_BIT = 0x04,
    KTX_TEXTURE_CREATE_CHECK_GLTF_BASISU_BIT = 0x08,
}
