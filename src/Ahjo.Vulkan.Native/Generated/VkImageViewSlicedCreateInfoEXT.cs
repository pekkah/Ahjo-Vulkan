namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewSlicedCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint sliceOffset;

    [NativeTypeName("uint32_t")]
    public uint sliceCount;
}
