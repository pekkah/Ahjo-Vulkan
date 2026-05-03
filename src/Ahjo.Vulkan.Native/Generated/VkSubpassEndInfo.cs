namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassEndInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;
}
