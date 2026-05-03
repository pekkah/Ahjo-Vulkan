namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderFmaFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFmaFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderFmaFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderFmaFloat64;
}
