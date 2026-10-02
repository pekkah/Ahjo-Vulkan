namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindVertexBuffer3InfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint setStride;

    public VkStridedDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;
}
