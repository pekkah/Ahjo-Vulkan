namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPartitionedAccelerationStructureInstancesInputNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBuildAccelerationStructureFlagsKHR")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint instanceCount;

    [NativeTypeName("uint32_t")]
    public uint maxInstancePerPartitionCount;

    [NativeTypeName("uint32_t")]
    public uint partitionCount;

    [NativeTypeName("uint32_t")]
    public uint maxInstanceInGlobalPartitionCount;
}
