namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceNonSeamlessCubeMapFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint nonSeamlessCubeMap;
}
