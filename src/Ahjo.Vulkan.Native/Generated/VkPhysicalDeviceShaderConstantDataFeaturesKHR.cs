namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderConstantDataFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderConstantData;
}
