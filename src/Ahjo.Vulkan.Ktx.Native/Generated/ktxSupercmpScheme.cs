namespace Ahjo.Vulkan.Ktx.Native;

[NativeTypeName("unsigned int")]
public enum ktxSupercmpScheme : uint
{
    KTX_SS_NONE = 0,
    KTX_SS_BASIS_LZ = 1,
    KTX_SS_ZSTD = 2,
    KTX_SS_ZLIB = 3,
    KTX_SS_BEGIN_RANGE = KTX_SS_NONE,
    KTX_SS_END_RANGE = KTX_SS_ZLIB,
    KTX_SS_BEGIN_VENDOR_RANGE = 0x10000,
    KTX_SS_END_VENDOR_RANGE = 0x1ffff,
    KTX_SS_BEGIN_RESERVED = 0x20000,
}
