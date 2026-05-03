namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderFloat8FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat8;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat8CooperativeMatrix;
}
