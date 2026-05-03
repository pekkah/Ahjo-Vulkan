namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceImageSubresourceInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkImageCreateInfo *")]
    public VkImageCreateInfo* pCreateInfo;

    [NativeTypeName("const VkImageSubresource2 *")]
    public VkImageSubresource2* pSubresource;
}
