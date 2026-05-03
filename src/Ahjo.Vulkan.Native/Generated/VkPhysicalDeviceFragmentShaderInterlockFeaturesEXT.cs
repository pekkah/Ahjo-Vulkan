namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentShaderInterlockFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint fragmentShaderSampleInterlock;

    [NativeTypeName("VkBool32")]
    public uint fragmentShaderPixelInterlock;

    [NativeTypeName("VkBool32")]
    public uint fragmentShaderShadingRateInterlock;
}
