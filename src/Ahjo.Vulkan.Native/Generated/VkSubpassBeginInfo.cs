namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassBeginInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSubpassContents contents;
}
