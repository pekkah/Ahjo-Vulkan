namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCopyMemoryIndirectFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint indirectMemoryCopy;

    [NativeTypeName("VkBool32")]
    public uint indirectMemoryToImageCopy;
}
