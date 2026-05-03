namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMeshShaderFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint taskShader;

    [NativeTypeName("VkBool32")]
    public uint meshShader;

    [NativeTypeName("VkBool32")]
    public uint multiviewMeshShader;

    [NativeTypeName("VkBool32")]
    public uint primitiveFragmentShadingRateMeshShader;

    [NativeTypeName("VkBool32")]
    public uint meshShaderQueries;
}
