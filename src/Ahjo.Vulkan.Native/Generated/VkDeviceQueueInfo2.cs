namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceQueueInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceQueueCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;

    [NativeTypeName("uint32_t")]
    public uint queueIndex;
}
