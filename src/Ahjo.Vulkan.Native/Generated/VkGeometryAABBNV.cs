namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeometryAABBNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* aabbData;

    [NativeTypeName("uint32_t")]
    public uint numAABBs;

    [NativeTypeName("uint32_t")]
    public uint stride;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;
}
