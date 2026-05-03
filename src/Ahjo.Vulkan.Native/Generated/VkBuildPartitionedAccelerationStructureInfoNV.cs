namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBuildPartitionedAccelerationStructureInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkPartitionedAccelerationStructureInstancesInputNV input;

    [NativeTypeName("VkDeviceAddress")]
    public ulong srcAccelerationStructureData;

    [NativeTypeName("VkDeviceAddress")]
    public ulong dstAccelerationStructureData;

    [NativeTypeName("VkDeviceAddress")]
    public ulong scratchData;

    [NativeTypeName("VkDeviceAddress")]
    public ulong srcInfos;

    [NativeTypeName("VkDeviceAddress")]
    public ulong srcInfosCount;
}
