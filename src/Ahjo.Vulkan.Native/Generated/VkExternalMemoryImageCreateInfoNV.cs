namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalMemoryImageCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkExternalMemoryHandleTypeFlagsNV")]
    public uint handleTypes;
}
