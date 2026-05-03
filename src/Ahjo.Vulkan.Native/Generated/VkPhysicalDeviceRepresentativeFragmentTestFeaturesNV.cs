namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRepresentativeFragmentTestFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint representativeFragmentTest;
}
