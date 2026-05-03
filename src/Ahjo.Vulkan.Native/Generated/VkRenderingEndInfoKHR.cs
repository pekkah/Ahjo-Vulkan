namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingEndInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;
}
