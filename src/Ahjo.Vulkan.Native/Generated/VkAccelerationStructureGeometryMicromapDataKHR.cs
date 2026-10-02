namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometryMicromapDataKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint usageCountsCount;

    [NativeTypeName("const VkMicromapUsageKHR *")]
    public VkMicromapUsageKHR* pUsageCounts;

    [NativeTypeName("const VkMicromapUsageKHR *const *")]
    public VkMicromapUsageKHR** ppUsageCounts;

    [NativeTypeName("VkDeviceAddress")]
    public ulong data;

    [NativeTypeName("VkDeviceAddress")]
    public ulong triangleArray;

    [NativeTypeName("VkDeviceSize")]
    public ulong triangleArrayStride;
}
