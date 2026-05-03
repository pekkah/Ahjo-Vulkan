namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMicromapBuildInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkMicromapTypeEXT type;

    [NativeTypeName("VkBuildMicromapFlagsEXT")]
    public uint flags;

    public VkBuildMicromapModeEXT mode;

    [NativeTypeName("VkMicromapEXT")]
    public VkMicromapEXT_T* dstMicromap;

    [NativeTypeName("uint32_t")]
    public uint usageCountsCount;

    [NativeTypeName("const VkMicromapUsageEXT *")]
    public VkMicromapUsageEXT* pUsageCounts;

    [NativeTypeName("const VkMicromapUsageEXT *const *")]
    public VkMicromapUsageEXT** ppUsageCounts;

    public VkDeviceOrHostAddressConstKHR data;

    public VkDeviceOrHostAddressKHR scratchData;

    public VkDeviceOrHostAddressConstKHR triangleArray;

    [NativeTypeName("VkDeviceSize")]
    public ulong triangleArrayStride;
}
