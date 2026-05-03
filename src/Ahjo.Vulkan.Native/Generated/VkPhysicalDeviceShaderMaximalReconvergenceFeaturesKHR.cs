namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderMaximalReconvergenceFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderMaximalReconvergence;
}
