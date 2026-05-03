namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBaseOutStructure
{
    public VkStructureType sType;

    [NativeTypeName("struct VkBaseOutStructure *")]
    public VkBaseOutStructure* pNext;
}
