namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkConditionalRenderingBeginInfo2EXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    [NativeTypeName("VkConditionalRenderingFlagsEXT")]
    public uint flags;
}
