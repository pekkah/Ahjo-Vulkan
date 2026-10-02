namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceInfoPropertiesINTEL
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint deviceIpVersionArch;

    [NativeTypeName("uint32_t")]
    public uint deviceIpVersionRelease;

    [NativeTypeName("uint32_t")]
    public uint deviceIpVersionRevision;
}
