namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewSampleWeightCreateInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkOffset2D filterCenter;

    public VkExtent2D filterSize;

    [NativeTypeName("uint32_t")]
    public uint numPhases;
}
