namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoProfileListInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint profileCount;

    [NativeTypeName("const VkVideoProfileInfoKHR *")]
    public VkVideoProfileInfoKHR* pProfiles;
}
