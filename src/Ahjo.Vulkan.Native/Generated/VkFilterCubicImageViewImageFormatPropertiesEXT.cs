namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFilterCubicImageViewImageFormatPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint filterCubic;

    [NativeTypeName("VkBool32")]
    public uint filterCubicMinmax;
}
