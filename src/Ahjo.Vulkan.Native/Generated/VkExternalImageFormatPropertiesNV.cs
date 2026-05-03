namespace Ahjo.Vulkan.Native;

public partial struct VkExternalImageFormatPropertiesNV
{
    public VkImageFormatProperties imageFormatProperties;

    [NativeTypeName("VkExternalMemoryFeatureFlagsNV")]
    public uint externalMemoryFeatures;

    [NativeTypeName("VkExternalMemoryHandleTypeFlagsNV")]
    public uint exportFromImportedHandleTypes;

    [NativeTypeName("VkExternalMemoryHandleTypeFlagsNV")]
    public uint compatibleHandleTypes;
}
