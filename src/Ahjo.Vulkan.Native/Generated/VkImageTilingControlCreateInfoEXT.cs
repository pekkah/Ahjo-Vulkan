namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageTilingControlCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkImageTilingControlEXT tilingControl;
}
