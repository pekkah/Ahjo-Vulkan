namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFenceCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkFenceCreateFlags")]
    public uint flags;
}
