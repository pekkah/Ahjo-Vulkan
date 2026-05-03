namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureCaptureDescriptorDataInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* accelerationStructure;

    [NativeTypeName("VkAccelerationStructureNV")]
    public VkAccelerationStructureNV_T* accelerationStructureNV;
}
