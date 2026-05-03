namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSparseImageMemoryBindInfo
{
    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    [NativeTypeName("uint32_t")]
    public uint bindCount;

    [NativeTypeName("const VkSparseImageMemoryBind *")]
    public VkSparseImageMemoryBind* pBinds;
}
