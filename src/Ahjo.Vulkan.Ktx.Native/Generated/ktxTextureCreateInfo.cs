namespace Ahjo.Vulkan.Ktx.Native;

public unsafe partial struct ktxTextureCreateInfo
{
    [NativeTypeName("ktx_uint32_t")]
    public uint glInternalformat;

    [NativeTypeName("ktx_uint32_t")]
    public uint vkFormat;

    [NativeTypeName("ktx_uint32_t *")]
    public uint* pDfd;

    [NativeTypeName("ktx_uint32_t")]
    public uint baseWidth;

    [NativeTypeName("ktx_uint32_t")]
    public uint baseHeight;

    [NativeTypeName("ktx_uint32_t")]
    public uint baseDepth;

    [NativeTypeName("ktx_uint32_t")]
    public uint numDimensions;

    [NativeTypeName("ktx_uint32_t")]
    public uint numLevels;

    [NativeTypeName("ktx_uint32_t")]
    public uint numLayers;

    [NativeTypeName("ktx_uint32_t")]
    public uint numFaces;

    [NativeTypeName("ktx_bool_t")]
    public bool isArray;

    [NativeTypeName("ktx_bool_t")]
    public bool generateMipmaps;
}
