namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryBarrierAccessFlags3KHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccessFlags3KHR")]
    public ulong srcAccessMask3;

    [NativeTypeName("VkAccessFlags3KHR")]
    public ulong dstAccessMask3;
}
