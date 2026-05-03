namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultCountsEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint addressInfoCount;

    [NativeTypeName("uint32_t")]
    public uint vendorInfoCount;

    [NativeTypeName("VkDeviceSize")]
    public ulong vendorBinarySize;
}
