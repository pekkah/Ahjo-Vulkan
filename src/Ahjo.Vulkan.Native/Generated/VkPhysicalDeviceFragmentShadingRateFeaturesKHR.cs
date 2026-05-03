namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentShadingRateFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineFragmentShadingRate;

    [NativeTypeName("VkBool32")]
    public uint primitiveFragmentShadingRate;

    [NativeTypeName("VkBool32")]
    public uint attachmentFragmentShadingRate;
}
