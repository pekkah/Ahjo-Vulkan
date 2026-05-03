namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSubgroupSizeControlFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint subgroupSizeControl;

    [NativeTypeName("VkBool32")]
    public uint computeFullSubgroups;
}
