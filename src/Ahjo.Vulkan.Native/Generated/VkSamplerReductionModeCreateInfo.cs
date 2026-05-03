namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerReductionModeCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSamplerReductionMode reductionMode;
}
