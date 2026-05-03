namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSetAccelerationStructureKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint accelerationStructureCount;

    [NativeTypeName("const VkAccelerationStructureKHR *")]
    public VkAccelerationStructureKHR_T** pAccelerationStructures;
}
