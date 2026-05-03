namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFrameBoundaryEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkFrameBoundaryFlagsEXT")]
    public uint flags;

    [NativeTypeName("uint64_t")]
    public ulong frameID;

    [NativeTypeName("uint32_t")]
    public uint imageCount;

    [NativeTypeName("const VkImage *")]
    public VkImage_T** pImages;

    [NativeTypeName("uint32_t")]
    public uint bufferCount;

    [NativeTypeName("const VkBuffer *")]
    public VkBuffer_T** pBuffers;

    [NativeTypeName("uint64_t")]
    public ulong tagName;

    [NativeTypeName("size_t")]
    public nuint tagSize;

    [NativeTypeName("const void *")]
    public void* pTag;
}
