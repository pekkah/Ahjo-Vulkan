namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageAlignmentControlFeaturesMESA
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageAlignmentControl;
}
