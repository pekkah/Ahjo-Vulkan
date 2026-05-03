namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceQueueCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceQueueCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;

    [NativeTypeName("uint32_t")]
    public uint queueCount;

    [NativeTypeName("const float *")]
    public float* pQueuePriorities;
}
