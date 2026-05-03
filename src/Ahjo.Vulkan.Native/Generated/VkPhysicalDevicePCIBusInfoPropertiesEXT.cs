namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePCIBusInfoPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint pciDomain;

    [NativeTypeName("uint32_t")]
    public uint pciBus;

    [NativeTypeName("uint32_t")]
    public uint pciDevice;

    [NativeTypeName("uint32_t")]
    public uint pciFunction;
}
