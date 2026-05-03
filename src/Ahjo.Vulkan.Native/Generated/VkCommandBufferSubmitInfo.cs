namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferSubmitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCommandBuffer")]
    public VkCommandBuffer_T* commandBuffer;

    [NativeTypeName("uint32_t")]
    public uint deviceMask;
}
