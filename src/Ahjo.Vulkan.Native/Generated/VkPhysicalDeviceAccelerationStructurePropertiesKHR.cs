namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceAccelerationStructurePropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong maxGeometryCount;

    [NativeTypeName("uint64_t")]
    public ulong maxInstanceCount;

    [NativeTypeName("uint64_t")]
    public ulong maxPrimitiveCount;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorAccelerationStructures;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindAccelerationStructures;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetAccelerationStructures;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindAccelerationStructures;

    [NativeTypeName("uint32_t")]
    public uint minAccelerationStructureScratchOffsetAlignment;
}
