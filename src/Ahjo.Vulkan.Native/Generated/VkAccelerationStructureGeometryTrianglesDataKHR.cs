namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometryTrianglesDataKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat vertexFormat;

    public VkDeviceOrHostAddressConstKHR vertexData;

    [NativeTypeName("VkDeviceSize")]
    public ulong vertexStride;

    [NativeTypeName("uint32_t")]
    public uint maxVertex;

    public VkIndexType indexType;

    public VkDeviceOrHostAddressConstKHR indexData;

    public VkDeviceOrHostAddressConstKHR transformData;
}
