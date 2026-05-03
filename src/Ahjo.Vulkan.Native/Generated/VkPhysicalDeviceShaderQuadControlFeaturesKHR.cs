namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderQuadControlFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderQuadControl;
}
