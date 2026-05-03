namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpaqueCaptureDataCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkHostAddressRangeConstEXT *")]
    public VkHostAddressRangeConstEXT* pData;
}
