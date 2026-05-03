namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBlitImageCubicWeightsInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkCubicFilterWeightsQCOM cubicWeights;
}
