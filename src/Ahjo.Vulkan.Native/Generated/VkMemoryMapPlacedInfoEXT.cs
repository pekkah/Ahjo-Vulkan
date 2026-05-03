namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryMapPlacedInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public void* pPlacedAddress;
}
