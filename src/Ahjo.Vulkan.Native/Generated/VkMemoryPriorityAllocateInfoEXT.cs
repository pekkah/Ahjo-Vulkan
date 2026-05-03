namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryPriorityAllocateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public float priority;
}
