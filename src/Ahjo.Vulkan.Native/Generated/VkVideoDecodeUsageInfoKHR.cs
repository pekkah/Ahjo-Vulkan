namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeUsageInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoDecodeUsageFlagsKHR")]
    public uint videoUsageHints;
}
