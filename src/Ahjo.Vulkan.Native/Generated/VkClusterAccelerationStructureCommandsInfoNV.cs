namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkClusterAccelerationStructureCommandsInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkClusterAccelerationStructureInputInfoNV input;

    [NativeTypeName("VkDeviceAddress")]
    public ulong dstImplicitData;

    [NativeTypeName("VkDeviceAddress")]
    public ulong scratchData;

    public VkStridedDeviceAddressRegionKHR dstAddressesArray;

    public VkStridedDeviceAddressRegionKHR dstSizesArray;

    public VkStridedDeviceAddressRegionKHR srcInfosArray;

    [NativeTypeName("VkDeviceAddress")]
    public ulong srcInfosCount;

    [NativeTypeName("VkClusterAccelerationStructureAddressResolutionFlagsNV")]
    public uint addressResolutionFlags;
}
