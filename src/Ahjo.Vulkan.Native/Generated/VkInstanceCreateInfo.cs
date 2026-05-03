namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkInstanceCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkInstanceCreateFlags")]
    public uint flags;

    [NativeTypeName("const VkApplicationInfo *")]
    public VkApplicationInfo* pApplicationInfo;

    [NativeTypeName("uint32_t")]
    public uint enabledLayerCount;

    [NativeTypeName("const char *const *")]
    public sbyte** ppEnabledLayerNames;

    [NativeTypeName("uint32_t")]
    public uint enabledExtensionCount;

    [NativeTypeName("const char *const *")]
    public sbyte** ppEnabledExtensionNames;
}
