namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderOCPMicroscalingTypesFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat4;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat6;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat8UnsignedE8M0;

    [NativeTypeName("VkBool32")]
    public uint shaderMXInt8;
}
