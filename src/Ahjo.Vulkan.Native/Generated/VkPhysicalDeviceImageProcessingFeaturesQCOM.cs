namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageProcessingFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint textureSampleWeighted;

    [NativeTypeName("VkBool32")]
    public uint textureBoxFilter;

    [NativeTypeName("VkBool32")]
    public uint textureBlockMatch;
}
