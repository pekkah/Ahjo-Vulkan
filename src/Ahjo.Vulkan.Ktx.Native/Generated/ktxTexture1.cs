namespace Ahjo.Vulkan.Ktx.Native;

public unsafe partial struct ktxTexture1
{
    public class_id classId;

    [NativeTypeName("struct ktxTexture_vtbl *")]
    public ktxTexture_vtbl* vtbl;

    [NativeTypeName("struct ktxTexture_vvtbl *")]
    public void* vvtbl;

    [NativeTypeName("struct ktxTexture_protected *")]
    public void* _protected;

    [NativeTypeName("ktx_bool_t")]
    public bool isArray;

    [NativeTypeName("ktx_bool_t")]
    public bool isCubemap;

    [NativeTypeName("ktx_bool_t")]
    public bool isCompressed;

    [NativeTypeName("ktx_bool_t")]
    public bool generateMipmaps;

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

    [NativeTypeName("struct ktxOrientation")]
    public ktxOrientation orientation;

    [NativeTypeName("ktxHashList")]
    public ktxKVListEntry* kvDataHead;

    [NativeTypeName("ktx_uint32_t")]
    public uint kvDataLen;

    [NativeTypeName("ktx_uint8_t *")]
    public byte* kvData;

    [NativeTypeName("ktx_size_t")]
    public nuint dataSize;

    [NativeTypeName("ktx_uint8_t *")]
    public byte* pData;

    [NativeTypeName("ktx_uint32_t")]
    public uint glFormat;

    [NativeTypeName("ktx_uint32_t")]
    public uint glInternalformat;

    [NativeTypeName("ktx_uint32_t")]
    public uint glBaseInternalformat;

    [NativeTypeName("ktx_uint32_t")]
    public uint glType;

    [NativeTypeName("struct ktxTexture1_private *")]
    public ktxTexture1_private* _private;

    public partial struct ktxTexture1_private
    {
    }
}
