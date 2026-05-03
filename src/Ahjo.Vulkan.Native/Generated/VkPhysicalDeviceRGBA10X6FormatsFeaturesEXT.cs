namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRGBA10X6FormatsFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint formatRgba10x6WithoutYCbCrSampler;
}
