namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderMixedFloatDotProductFeaturesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderMixedFloatDotProductFloat16AccFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderMixedFloatDotProductFloat16AccFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderMixedFloatDotProductBFloat16Acc;

    [NativeTypeName("VkBool32")]
    public uint shaderMixedFloatDotProductFloat8AccFloat32;
}
