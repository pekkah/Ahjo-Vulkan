namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubresourceHostMemcpySize
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
