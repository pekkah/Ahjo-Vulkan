namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderFloat16Int8Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderInt8;
}
