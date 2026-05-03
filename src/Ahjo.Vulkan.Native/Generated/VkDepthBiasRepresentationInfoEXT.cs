namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDepthBiasRepresentationInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDepthBiasRepresentationEXT depthBiasRepresentation;

    [NativeTypeName("VkBool32")]
    public uint depthBiasExact;
}
