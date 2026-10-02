namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoEncodeFeedback2FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoEncodeFeedback2;
}
