namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureDeviceAddressInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* accelerationStructure;
}
