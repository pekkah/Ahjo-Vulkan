namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSetPartitionedAccelerationStructureNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint accelerationStructureCount;

    [NativeTypeName("const VkDeviceAddress *")]
    public ulong* pAccelerationStructures;
}
