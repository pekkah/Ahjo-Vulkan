namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorBufferDensityMapPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint combinedImageSamplerDensityMapDescriptorSize;
}
