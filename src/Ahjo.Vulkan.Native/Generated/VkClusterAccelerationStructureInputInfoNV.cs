namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkClusterAccelerationStructureInputInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxAccelerationStructureCount;

    [NativeTypeName("VkBuildAccelerationStructureFlagsKHR")]
    public uint flags;

    public VkClusterAccelerationStructureOpTypeNV opType;

    public VkClusterAccelerationStructureOpModeNV opMode;

    public VkClusterAccelerationStructureOpInputNV opInput;
}
