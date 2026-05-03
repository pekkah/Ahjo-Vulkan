namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMicromapBuildSizesInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong micromapSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong buildScratchSize;

    [NativeTypeName("VkBool32")]
    public uint discardable;
}
