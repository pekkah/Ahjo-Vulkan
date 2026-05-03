namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSparseBufferMemoryBindInfo
{
    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    [NativeTypeName("uint32_t")]
    public uint bindCount;

    [NativeTypeName("const VkSparseMemoryBind *")]
    public VkSparseMemoryBind* pBinds;
}
