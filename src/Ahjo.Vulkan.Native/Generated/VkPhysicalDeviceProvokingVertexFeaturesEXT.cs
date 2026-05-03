namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceProvokingVertexFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint provokingVertexLast;

    [NativeTypeName("VkBool32")]
    public uint transformFeedbackPreservesProvokingVertex;
}
