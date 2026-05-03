namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExportSemaphoreCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkExternalSemaphoreHandleTypeFlags")]
    public uint handleTypes;
}
