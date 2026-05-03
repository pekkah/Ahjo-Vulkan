namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoEncodeAV1FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoEncodeAV1;
}
