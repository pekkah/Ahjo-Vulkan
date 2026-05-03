namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceImageMemoryRequirements
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkImageCreateInfo *")]
    public VkImageCreateInfo* pCreateInfo;

    public VkImageAspectFlagBits planeAspect;
}
