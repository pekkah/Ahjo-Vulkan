namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyAccelerationStructureToMemoryInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* src;

    public VkDeviceOrHostAddressKHR dst;

    public VkCopyAccelerationStructureModeKHR mode;
}
