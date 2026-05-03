namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryAllocateFlagsInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMemoryAllocateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint deviceMask;
}
