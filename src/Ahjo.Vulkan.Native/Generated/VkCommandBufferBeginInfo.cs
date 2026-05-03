namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferBeginInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCommandBufferUsageFlags")]
    public uint flags;

    [NativeTypeName("const VkCommandBufferInheritanceInfo *")]
    public VkCommandBufferInheritanceInfo* pInheritanceInfo;
}
