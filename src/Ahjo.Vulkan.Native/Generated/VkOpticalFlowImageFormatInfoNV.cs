namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpticalFlowImageFormatInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkOpticalFlowUsageFlagsNV")]
    public uint usage;
}
