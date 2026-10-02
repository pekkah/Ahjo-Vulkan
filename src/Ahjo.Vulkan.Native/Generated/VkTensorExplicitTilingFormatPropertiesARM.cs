namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorExplicitTilingFormatPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong brick16TilingTensorFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong brick8TilingTensorFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong brick4TilingTensorFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong blockUTilingTensorFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong blockU64kTilingTensorFeatures;
}
