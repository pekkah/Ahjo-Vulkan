namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceAntiLagFeaturesAMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint antiLag;
}
