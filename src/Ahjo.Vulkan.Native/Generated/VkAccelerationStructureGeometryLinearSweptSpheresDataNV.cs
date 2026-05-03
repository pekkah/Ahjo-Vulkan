namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometryLinearSweptSpheresDataNV
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

    public VkRayTracingLssIndexingModeNV indexingMode;

    public VkRayTracingLssPrimitiveEndCapsModeNV endCapsMode;
}
