namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFormatProperties3
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong linearTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong optimalTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong bufferFeatures;
}
