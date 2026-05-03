namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeSessionIntraRefreshCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkVideoEncodeIntraRefreshModeFlagBitsKHR intraRefreshMode;
}
