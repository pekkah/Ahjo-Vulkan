namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkValidationFeaturesEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint enabledValidationFeatureCount;

    [NativeTypeName("const VkValidationFeatureEnableEXT *")]
    public VkValidationFeatureEnableEXT* pEnabledValidationFeatures;

    [NativeTypeName("uint32_t")]
    public uint disabledValidationFeatureCount;

    [NativeTypeName("const VkValidationFeatureDisableEXT *")]
    public VkValidationFeatureDisableEXT* pDisabledValidationFeatures;
}
