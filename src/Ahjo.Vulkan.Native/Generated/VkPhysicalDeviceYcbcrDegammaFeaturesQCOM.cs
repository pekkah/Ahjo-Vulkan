namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceYcbcrDegammaFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint ycbcrDegamma;
}
