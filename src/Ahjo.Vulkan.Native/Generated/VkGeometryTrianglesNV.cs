namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeometryTrianglesNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* vertexData;

    [NativeTypeName("VkDeviceSize")]
    public ulong vertexOffset;

    [NativeTypeName("uint32_t")]
    public uint vertexCount;

    [NativeTypeName("VkDeviceSize")]
    public ulong vertexStride;

    public VkFormat vertexFormat;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* indexData;

    [NativeTypeName("VkDeviceSize")]
    public ulong indexOffset;

    [NativeTypeName("uint32_t")]
    public uint indexCount;

    public VkIndexType indexType;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* transformData;

    [NativeTypeName("VkDeviceSize")]
    public ulong transformOffset;
}
