namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewMinLodCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public float minLod;
}
