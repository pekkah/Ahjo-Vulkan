namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint queueCreateInfoCount;

    [NativeTypeName("const VkDeviceQueueCreateInfo *")]
    public VkDeviceQueueCreateInfo* pQueueCreateInfos;

    [NativeTypeName("uint32_t")]
    public uint enabledLayerCount;

    [NativeTypeName("const char *const *")]
    public sbyte** ppEnabledLayerNames;

    [NativeTypeName("uint32_t")]
    public uint enabledExtensionCount;

    [NativeTypeName("const char *const *")]
    public sbyte** ppEnabledExtensionNames;

    [NativeTypeName("const VkPhysicalDeviceFeatures *")]
    public VkPhysicalDeviceFeatures* pEnabledFeatures;
}
