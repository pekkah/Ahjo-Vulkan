namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyAccelerationStructureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* src;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* dst;

    public VkCopyAccelerationStructureModeKHR mode;
}
