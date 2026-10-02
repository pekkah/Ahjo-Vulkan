namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphOpticalFlowImageFormatInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDataGraphOpticalFlowImageUsageFlagsARM")]
    public uint usage;
}
