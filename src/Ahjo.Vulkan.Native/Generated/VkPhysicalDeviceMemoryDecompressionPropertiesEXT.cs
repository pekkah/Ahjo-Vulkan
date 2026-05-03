namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMemoryDecompressionPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkMemoryDecompressionMethodFlagsEXT")]
    public ulong decompressionMethods;

    [NativeTypeName("uint64_t")]
    public ulong maxDecompressionIndirectCount;
}
