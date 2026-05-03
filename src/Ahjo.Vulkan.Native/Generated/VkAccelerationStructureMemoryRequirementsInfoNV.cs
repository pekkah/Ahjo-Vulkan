namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureMemoryRequirementsInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkAccelerationStructureMemoryRequirementsTypeNV type;

    [NativeTypeName("VkAccelerationStructureNV")]
    public VkAccelerationStructureNV_T* accelerationStructure;
}
