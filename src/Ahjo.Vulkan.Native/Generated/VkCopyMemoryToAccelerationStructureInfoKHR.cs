namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMemoryToAccelerationStructureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceOrHostAddressConstKHR src;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* dst;

    public VkCopyAccelerationStructureModeKHR mode;
}
