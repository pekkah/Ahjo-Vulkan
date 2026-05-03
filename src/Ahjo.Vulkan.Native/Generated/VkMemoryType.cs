namespace Ahjo.Vulkan.Native;

public partial struct VkMemoryType
{
    [NativeTypeName("VkMemoryPropertyFlags")]
    public uint propertyFlags;

    [NativeTypeName("uint32_t")]
    public uint heapIndex;
}
