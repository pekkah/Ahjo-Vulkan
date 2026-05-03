namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCalibratedTimestampInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkTimeDomainKHR timeDomain;
}
