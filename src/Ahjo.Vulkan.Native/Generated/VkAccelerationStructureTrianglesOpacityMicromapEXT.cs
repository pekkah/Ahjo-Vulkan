namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureTrianglesOpacityMicromapEXT
{
    public VkStructureType sType;

    public void* pNext;

    public VkIndexType indexType;

    public VkDeviceOrHostAddressConstKHR indexBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong indexStride;

    [NativeTypeName("uint32_t")]
    public uint baseTriangle;

    [NativeTypeName("uint32_t")]
    public uint usageCountsCount;

    [NativeTypeName("const VkMicromapUsageEXT *")]
    public VkMicromapUsageEXT* pUsageCounts;

    [NativeTypeName("const VkMicromapUsageEXT *const *")]
    public VkMicromapUsageEXT** ppUsageCounts;

    [NativeTypeName("VkMicromapEXT")]
    public VkMicromapEXT_T* micromap;
}
