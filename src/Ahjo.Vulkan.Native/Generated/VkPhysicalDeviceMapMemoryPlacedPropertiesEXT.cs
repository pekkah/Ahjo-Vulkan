namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMapMemoryPlacedPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong minPlacedMemoryMapAlignment;
}
