namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMapPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D minFragmentDensityTexelSize;

    public VkExtent2D maxFragmentDensityTexelSize;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityInvocations;
}
