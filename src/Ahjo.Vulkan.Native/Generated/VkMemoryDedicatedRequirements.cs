namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryDedicatedRequirements
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint prefersDedicatedAllocation;

    [NativeTypeName("VkBool32")]
    public uint requiresDedicatedAllocation;
}
