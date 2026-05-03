namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorHeapPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong samplerHeapAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong resourceHeapAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxSamplerHeapSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxResourceHeapSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong minSamplerHeapReservedRange;

    [NativeTypeName("VkDeviceSize")]
    public ulong minSamplerHeapReservedRangeWithEmbedded;

    [NativeTypeName("VkDeviceSize")]
    public ulong minResourceHeapReservedRange;

    [NativeTypeName("VkDeviceSize")]
    public ulong samplerDescriptorSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong imageDescriptorSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong bufferDescriptorSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong samplerDescriptorAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong imageDescriptorAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong bufferDescriptorAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxPushDataSize;

    [NativeTypeName("size_t")]
    public nuint imageCaptureReplayOpaqueDataSize;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorHeapEmbeddedSamplers;

    [NativeTypeName("uint32_t")]
    public uint samplerYcbcrConversionCount;

    [NativeTypeName("VkBool32")]
    public uint sparseDescriptorHeaps;

    [NativeTypeName("VkBool32")]
    public uint protectedDescriptorHeaps;
}
