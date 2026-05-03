namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalFenceInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExternalFenceHandleTypeFlagBits handleType;
}
