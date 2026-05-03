namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainTimeDomainPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint timeDomainCount;

    public VkTimeDomainKHR* pTimeDomains;

    [NativeTypeName("uint64_t *")]
    public ulong* pTimeDomainIds;
}
