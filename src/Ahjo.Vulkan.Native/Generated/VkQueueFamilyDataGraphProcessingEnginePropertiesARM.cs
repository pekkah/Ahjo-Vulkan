namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyDataGraphProcessingEnginePropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkExternalSemaphoreHandleTypeFlags")]
    public uint foreignSemaphoreHandleTypes;

    [NativeTypeName("VkExternalMemoryHandleTypeFlags")]
    public uint foreignMemoryHandleTypes;
}
