namespace Ahjo.Vulkan.Native;

public partial struct VkExternalMemoryProperties
{
    [NativeTypeName("VkExternalMemoryFeatureFlags")]
    public uint externalMemoryFeatures;

    [NativeTypeName("VkExternalMemoryHandleTypeFlags")]
    public uint exportFromImportedHandleTypes;

    [NativeTypeName("VkExternalMemoryHandleTypeFlags")]
    public uint compatibleHandleTypes;
}
