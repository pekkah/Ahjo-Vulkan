namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDedicatedAllocationBufferCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dedicatedAllocation;
}
