namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentTimingFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentTiming;

    [NativeTypeName("VkBool32")]
    public uint presentAtAbsoluteTime;

    [NativeTypeName("VkBool32")]
    public uint presentAtRelativeTime;
}
