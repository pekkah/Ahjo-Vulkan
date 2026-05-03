namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageCompressionControlEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageCompressionFlagsEXT")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint compressionControlPlaneCount;

    [NativeTypeName("VkImageCompressionFixedRateFlagsEXT *")]
    public uint* pFixedRateFlags;
}
