namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMeshShaderFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint taskShader;

    [NativeTypeName("VkBool32")]
    public uint meshShader;
}
