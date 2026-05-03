namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageViewMinLodFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint minLod;
}
