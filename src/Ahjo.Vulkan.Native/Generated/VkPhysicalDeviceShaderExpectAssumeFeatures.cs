namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderExpectAssumeFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderExpectAssume;
}
