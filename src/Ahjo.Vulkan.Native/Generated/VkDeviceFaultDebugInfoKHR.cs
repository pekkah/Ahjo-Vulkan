namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultDebugInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint vendorBinarySize;

    public void* pVendorBinaryData;
}
