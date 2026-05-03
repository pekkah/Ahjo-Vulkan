namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBlendOperationAdvancedPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint advancedBlendMaxColorAttachments;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendIndependentBlend;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendNonPremultipliedSrcColor;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendNonPremultipliedDstColor;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendCorrelatedOverlap;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendAllOperations;
}
