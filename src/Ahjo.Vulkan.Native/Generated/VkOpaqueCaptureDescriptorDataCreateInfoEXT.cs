namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpaqueCaptureDescriptorDataCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const void *")]
    public void* opaqueCaptureDescriptorData;
}
