namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDepthBiasInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public float depthBiasConstantFactor;

    public float depthBiasClamp;

    public float depthBiasSlopeFactor;
}
