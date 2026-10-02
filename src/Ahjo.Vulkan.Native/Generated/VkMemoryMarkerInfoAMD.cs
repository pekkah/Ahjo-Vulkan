namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryMarkerInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags2KHR")]
    public ulong stage;

    public VkDeviceAddressRangeKHR dstRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint dstFlags;

    [NativeTypeName("uint32_t")]
    public uint marker;
}
