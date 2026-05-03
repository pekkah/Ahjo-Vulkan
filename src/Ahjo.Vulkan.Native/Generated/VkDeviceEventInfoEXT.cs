namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceEventInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceEventTypeEXT deviceEvent;
}
