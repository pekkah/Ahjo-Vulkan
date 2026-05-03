namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometrySpheresDataNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat vertexFormat;

    public VkDeviceOrHostAddressConstKHR vertexData;

    [NativeTypeName("VkDeviceSize")]
    public ulong vertexStride;

    public VkFormat radiusFormat;

    public VkDeviceOrHostAddressConstKHR radiusData;

    [NativeTypeName("VkDeviceSize")]
    public ulong radiusStride;

    public VkIndexType indexType;

    public VkDeviceOrHostAddressConstKHR indexData;

    [NativeTypeName("VkDeviceSize")]
    public ulong indexStride;
}
