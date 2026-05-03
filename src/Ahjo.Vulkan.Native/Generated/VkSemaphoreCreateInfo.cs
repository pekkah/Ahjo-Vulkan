namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphoreCreateFlags")]
    public uint flags;
}
