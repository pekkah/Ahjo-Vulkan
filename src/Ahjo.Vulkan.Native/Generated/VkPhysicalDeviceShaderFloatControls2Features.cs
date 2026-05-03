namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderFloatControls2Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFloatControls2;
}
