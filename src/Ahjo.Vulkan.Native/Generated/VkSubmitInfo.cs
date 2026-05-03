namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubmitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pWaitSemaphores;

    [NativeTypeName("const VkPipelineStageFlags *")]
    public uint* pWaitDstStageMask;

    [NativeTypeName("uint32_t")]
    public uint commandBufferCount;

    [NativeTypeName("const VkCommandBuffer *")]
    public VkCommandBuffer_T** pCommandBuffers;

    [NativeTypeName("uint32_t")]
    public uint signalSemaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pSignalSemaphores;
}
