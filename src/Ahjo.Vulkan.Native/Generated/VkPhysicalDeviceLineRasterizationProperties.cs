namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLineRasterizationProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint lineSubPixelPrecisionBits;
}
