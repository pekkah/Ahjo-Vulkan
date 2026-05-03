namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalMemoryHostPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong minImportedHostPointerAlignment;
}
