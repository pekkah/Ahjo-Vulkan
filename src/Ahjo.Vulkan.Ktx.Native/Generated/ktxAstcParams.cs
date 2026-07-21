using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Ktx.Native;

public partial struct ktxAstcParams
{
    [NativeTypeName("ktx_uint32_t")]
    public uint structSize;

    [NativeTypeName("ktx_bool_t")]
    public bool verbose;

    [NativeTypeName("ktx_uint32_t")]
    public uint threadCount;

    [NativeTypeName("ktx_uint32_t")]
    public uint blockDimension;

    [NativeTypeName("ktx_uint32_t")]
    public uint mode;

    [NativeTypeName("ktx_uint32_t")]
    public uint qualityLevel;

    [NativeTypeName("ktx_bool_t")]
    public bool normalMap;

    [NativeTypeName("ktx_bool_t")]
    public bool perceptual;

    [NativeTypeName("char[4]")]
    public _inputSwizzle_e__FixedBuffer inputSwizzle;

    [InlineArray(4)]
    public partial struct _inputSwizzle_e__FixedBuffer
    {
        public sbyte e0;
    }
}
