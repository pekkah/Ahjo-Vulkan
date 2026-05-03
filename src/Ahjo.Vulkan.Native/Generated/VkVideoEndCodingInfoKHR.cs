namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEndCodingInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEndCodingFlagsKHR")]
    public uint flags;
}
