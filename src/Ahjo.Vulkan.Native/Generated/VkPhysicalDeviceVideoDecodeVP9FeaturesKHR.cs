namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoDecodeVP9FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoDecodeVP9;
}
