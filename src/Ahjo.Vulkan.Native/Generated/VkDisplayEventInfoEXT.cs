namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayEventInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDisplayEventTypeEXT displayEvent;
}
