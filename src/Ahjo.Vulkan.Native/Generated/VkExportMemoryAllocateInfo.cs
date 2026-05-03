namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExportMemoryAllocateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkExternalMemoryHandleTypeFlags")]
    public uint handleTypes;
}
