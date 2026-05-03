namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePrimitivesGeneratedQueryFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint primitivesGeneratedQuery;

    [NativeTypeName("VkBool32")]
    public uint primitivesGeneratedQueryWithRasterizerDiscard;

    [NativeTypeName("VkBool32")]
    public uint primitivesGeneratedQueryWithNonZeroStreams;
}
