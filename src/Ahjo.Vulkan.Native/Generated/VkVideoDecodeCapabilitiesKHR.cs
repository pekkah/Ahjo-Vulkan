namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoDecodeCapabilityFlagsKHR")]
    public uint flags;
}
