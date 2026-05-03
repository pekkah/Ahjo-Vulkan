namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalMemoryAcquireUnmodifiedEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint acquireUnmodifiedMemory;
}
