namespace Ahjo.Vulkan.Native;

public partial struct VkImageFormatProperties
{
    public VkExtent3D maxExtent;

    [NativeTypeName("uint32_t")]
    public uint maxMipLevels;

    [NativeTypeName("uint32_t")]
    public uint maxArrayLayers;

    [NativeTypeName("VkSampleCountFlags")]
    public uint sampleCounts;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxResourceSize;
}
