namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSetAccelerationStructureNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint accelerationStructureCount;

    [NativeTypeName("const VkAccelerationStructureNV *")]
    public VkAccelerationStructureNV_T** pAccelerationStructures;
}
