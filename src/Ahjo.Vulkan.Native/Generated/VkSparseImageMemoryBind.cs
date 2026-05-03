namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSparseImageMemoryBind
{
    public VkImageSubresource subresource;

    public VkOffset3D offset;

    public VkExtent3D extent;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong memoryOffset;

    [NativeTypeName("VkSparseMemoryBindFlags")]
    public uint flags;
}
