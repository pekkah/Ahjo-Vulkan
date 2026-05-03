namespace Ahjo.Vulkan.Native;

public partial struct VkSparseImageMemoryRequirements
{
    public VkSparseImageFormatProperties formatProperties;

    [NativeTypeName("uint32_t")]
    public uint imageMipTailFirstLod;

    [NativeTypeName("VkDeviceSize")]
    public ulong imageMipTailSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong imageMipTailOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong imageMipTailStride;
}
