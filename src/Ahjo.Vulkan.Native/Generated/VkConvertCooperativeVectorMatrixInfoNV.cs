namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkConvertCooperativeVectorMatrixInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint srcSize;

    public VkDeviceOrHostAddressConstKHR srcData;

    [NativeTypeName("size_t *")]
    public nuint* pDstSize;

    public VkDeviceOrHostAddressKHR dstData;

    public VkComponentTypeKHR srcComponentType;

    public VkComponentTypeKHR dstComponentType;

    [NativeTypeName("uint32_t")]
    public uint numRows;

    [NativeTypeName("uint32_t")]
    public uint numColumns;

    public VkCooperativeVectorMatrixLayoutNV srcLayout;

    [NativeTypeName("size_t")]
    public nuint srcStride;

    public VkCooperativeVectorMatrixLayoutNV dstLayout;

    [NativeTypeName("size_t")]
    public nuint dstStride;
}
