namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderTileImageFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageColorReadAccess;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageDepthReadAccess;

    [NativeTypeName("VkBool32")]
    public uint shaderTileImageStencilReadAccess;
}
