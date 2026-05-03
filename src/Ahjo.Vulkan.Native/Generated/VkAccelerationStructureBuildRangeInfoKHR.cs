namespace Ahjo.Vulkan.Native;

public partial struct VkAccelerationStructureBuildRangeInfoKHR
{
    [NativeTypeName("uint32_t")]
    public uint primitiveCount;

    [NativeTypeName("uint32_t")]
    public uint primitiveOffset;

    [NativeTypeName("uint32_t")]
    public uint firstVertex;

    [NativeTypeName("uint32_t")]
    public uint transformOffset;
}
