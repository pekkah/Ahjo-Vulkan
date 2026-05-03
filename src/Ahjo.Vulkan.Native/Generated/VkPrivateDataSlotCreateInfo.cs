namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPrivateDataSlotCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPrivateDataSlotCreateFlags")]
    public uint flags;
}
