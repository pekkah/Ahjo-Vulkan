namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupSubmitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pWaitSemaphoreDeviceIndices;

    [NativeTypeName("uint32_t")]
    public uint commandBufferCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pCommandBufferDeviceMasks;

    [NativeTypeName("uint32_t")]
    public uint signalSemaphoreCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pSignalSemaphoreDeviceIndices;
}
