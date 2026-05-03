namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDependencyInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDependencyFlags")]
    public uint dependencyFlags;

    [NativeTypeName("uint32_t")]
    public uint memoryBarrierCount;

    [NativeTypeName("const VkMemoryBarrier2 *")]
    public VkMemoryBarrier2* pMemoryBarriers;

    [NativeTypeName("uint32_t")]
    public uint bufferMemoryBarrierCount;

    [NativeTypeName("const VkBufferMemoryBarrier2 *")]
    public VkBufferMemoryBarrier2* pBufferMemoryBarriers;

    [NativeTypeName("uint32_t")]
    public uint imageMemoryBarrierCount;

    [NativeTypeName("const VkImageMemoryBarrier2 *")]
    public VkImageMemoryBarrier2* pImageMemoryBarriers;
}
