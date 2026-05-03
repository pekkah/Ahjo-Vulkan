namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiviewProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxMultiviewViewCount;

    [NativeTypeName("uint32_t")]
    public uint maxMultiviewInstanceIndex;
}
