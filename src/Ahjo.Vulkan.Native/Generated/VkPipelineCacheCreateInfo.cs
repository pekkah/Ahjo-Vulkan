namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCacheCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCacheCreateFlags")]
    public uint flags;

    [NativeTypeName("size_t")]
    public nuint initialDataSize;

    [NativeTypeName("const void *")]
    public void* pInitialData;
}
