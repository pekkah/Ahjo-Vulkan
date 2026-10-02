namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceMemoryCopyKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeKHR srcRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint srcFlags;

    public VkDeviceAddressRangeKHR dstRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint dstFlags;
}
