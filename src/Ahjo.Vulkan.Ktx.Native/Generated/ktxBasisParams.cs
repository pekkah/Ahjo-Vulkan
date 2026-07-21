using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Ktx.Native;

public partial struct ktxBasisParams
{
    [NativeTypeName("ktx_uint32_t")]
    public uint structSize;

    [NativeTypeName("ktx_bool_t")]
    public bool uastc;

    [NativeTypeName("ktx_bool_t")]
    public bool verbose;

    [NativeTypeName("ktx_bool_t")]
    public bool noSSE;

    [NativeTypeName("ktx_uint32_t")]
    public uint threadCount;

    [NativeTypeName("ktx_uint32_t")]
    public uint compressionLevel;

    [NativeTypeName("ktx_uint32_t")]
    public uint qualityLevel;

    [NativeTypeName("ktx_uint32_t")]
    public uint maxEndpoints;

    public float endpointRDOThreshold;

    [NativeTypeName("ktx_uint32_t")]
    public uint maxSelectors;

    public float selectorRDOThreshold;

    [NativeTypeName("char[4]")]
    public _inputSwizzle_e__FixedBuffer inputSwizzle;

    [NativeTypeName("ktx_bool_t")]
    public bool normalMap;

    [NativeTypeName("ktx_bool_t")]
    public bool separateRGToRGB_A;

    [NativeTypeName("ktx_bool_t")]
    public bool preSwizzle;

    [NativeTypeName("ktx_bool_t")]
    public bool noEndpointRDO;

    [NativeTypeName("ktx_bool_t")]
    public bool noSelectorRDO;

    [NativeTypeName("ktx_pack_uastc_flags")]
    public uint uastcFlags;

    [NativeTypeName("ktx_bool_t")]
    public bool uastcRDO;

    public float uastcRDOQualityScalar;

    [NativeTypeName("ktx_uint32_t")]
    public uint uastcRDODictSize;

    public float uastcRDOMaxSmoothBlockErrorScale;

    public float uastcRDOMaxSmoothBlockStdDev;

    [NativeTypeName("ktx_bool_t")]
    public bool uastcRDODontFavorSimplerModes;

    [NativeTypeName("ktx_bool_t")]
    public bool uastcRDONoMultithreading;

    [InlineArray(4)]
    public partial struct _inputSwizzle_e__FixedBuffer
    {
        public sbyte e0;
    }
}
