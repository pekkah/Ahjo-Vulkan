namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceHostImageCopyFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint hostImageCopy;
}
