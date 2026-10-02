namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFormatProperties4KHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkFormatFeatureFlags4KHR")]
    public ulong linearTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags4KHR")]
    public ulong optimalTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags4KHR")]
    public ulong bufferFeatures;
}
