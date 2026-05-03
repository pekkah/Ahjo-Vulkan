namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalMemoryImageCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkExternalMemoryHandleTypeFlags")]
    public uint handleTypes;
}
