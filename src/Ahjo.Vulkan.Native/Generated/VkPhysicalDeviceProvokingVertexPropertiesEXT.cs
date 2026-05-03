namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceProvokingVertexPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint provokingVertexModePerPipeline;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackPreservesTriangleFanProvokingVertex;
}
