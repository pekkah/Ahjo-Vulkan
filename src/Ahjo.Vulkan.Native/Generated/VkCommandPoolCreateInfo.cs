namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandPoolCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCommandPoolCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;
}
