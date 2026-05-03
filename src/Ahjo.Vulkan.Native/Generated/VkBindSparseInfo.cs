namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindSparseInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pWaitSemaphores;

    [NativeTypeName("uint32_t")]
    public uint bufferBindCount;

    [NativeTypeName("const VkSparseBufferMemoryBindInfo *")]
    public VkSparseBufferMemoryBindInfo* pBufferBinds;

    [NativeTypeName("uint32_t")]
    public uint imageOpaqueBindCount;

    [NativeTypeName("const VkSparseImageOpaqueMemoryBindInfo *")]
    public VkSparseImageOpaqueMemoryBindInfo* pImageOpaqueBinds;

    [NativeTypeName("uint32_t")]
    public uint imageBindCount;

    [NativeTypeName("const VkSparseImageMemoryBindInfo *")]
    public VkSparseImageMemoryBindInfo* pImageBinds;

    [NativeTypeName("uint32_t")]
    public uint signalSemaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pSignalSemaphores;
}
