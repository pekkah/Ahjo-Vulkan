namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderTileImagePropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageCoherentReadAccelerated;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageReadSampleFromPixelRateInvocation;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageReadFromHelperInvocation;
}
