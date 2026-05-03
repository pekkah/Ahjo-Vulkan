namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExportMemoryAllocateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkExternalMemoryHandleTypeFlagsNV")]
    public uint handleTypes;
}
