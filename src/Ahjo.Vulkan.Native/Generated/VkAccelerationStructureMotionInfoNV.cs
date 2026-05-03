namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureMotionInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxInstances;

    [NativeTypeName("VkAccelerationStructureMotionInfoFlagsNV")]
    public uint flags;
}
