namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderInstrumentationFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderInstrumentation;
}
