namespace Ahjo.Vulkan.Native;

public partial struct VkBuildPartitionedAccelerationStructureIndirectCommandNV
{
    public VkPartitionedAccelerationStructureOpTypeNV opType;

    [NativeTypeName("uint32_t")]
    public uint argCount;

    public VkStridedDeviceAddressNV argData;
}
