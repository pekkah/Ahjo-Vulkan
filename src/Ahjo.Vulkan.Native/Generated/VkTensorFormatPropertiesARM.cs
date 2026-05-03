namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorFormatPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong optimalTilingTensorFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong linearTilingTensorFeatures;
}
