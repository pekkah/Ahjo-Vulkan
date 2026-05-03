namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryHostPointerPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint memoryTypeBits;
}
