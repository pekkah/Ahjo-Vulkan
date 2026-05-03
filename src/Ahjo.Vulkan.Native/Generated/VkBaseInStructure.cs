namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBaseInStructure
{
    public VkStructureType sType;

    [NativeTypeName("const struct VkBaseInStructure *")]
    public VkBaseInStructure* pNext;
}
