namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalImageFormatInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExternalMemoryHandleTypeFlagBits handleType;
}
