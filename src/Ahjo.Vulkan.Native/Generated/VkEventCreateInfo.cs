namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkEventCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkEventCreateFlags")]
    public uint flags;
}
