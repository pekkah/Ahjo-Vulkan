namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCopyMemoryIndirectFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint indirectCopy;
}
