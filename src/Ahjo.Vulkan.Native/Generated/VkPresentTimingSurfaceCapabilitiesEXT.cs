namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentTimingSurfaceCapabilitiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentTimingSupported;

    [NativeTypeName("VkBool32")]
    public uint presentAtAbsoluteTimeSupported;

    [NativeTypeName("VkBool32")]
    public uint presentAtRelativeTimeSupported;

    [NativeTypeName("VkPresentStageFlagsEXT")]
    public uint presentStageQueries;
}
