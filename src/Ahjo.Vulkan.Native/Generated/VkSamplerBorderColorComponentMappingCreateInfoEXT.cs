namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerBorderColorComponentMappingCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkComponentMapping components;

    [NativeTypeName("VkBool32")]
    public uint srgb;
}
