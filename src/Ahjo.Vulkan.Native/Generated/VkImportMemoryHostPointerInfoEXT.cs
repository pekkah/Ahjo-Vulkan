namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImportMemoryHostPointerInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExternalMemoryHandleTypeFlagBits handleType;

    public void* pHostPointer;
}
