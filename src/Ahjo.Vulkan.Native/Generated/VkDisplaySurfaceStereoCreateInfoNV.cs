namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplaySurfaceStereoCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDisplaySurfaceStereoTypeNV stereoType;
}
