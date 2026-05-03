namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSparseImageOpaqueMemoryBindInfo
{
    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    [NativeTypeName("uint32_t")]
    public uint bindCount;

    [NativeTypeName("const VkSparseMemoryBind *")]
    public VkSparseMemoryBind* pBinds;
}
