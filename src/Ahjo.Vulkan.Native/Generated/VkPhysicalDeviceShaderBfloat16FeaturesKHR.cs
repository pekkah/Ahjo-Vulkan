namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderBfloat16FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderBFloat16Type;

    [NativeTypeName("VkBool32")]
    public uint shaderBFloat16DotProduct;

    [NativeTypeName("VkBool32")]
    public uint shaderBFloat16CooperativeMatrix;
}
