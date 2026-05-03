namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLegacyDitheringFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint legacyDithering;
}
