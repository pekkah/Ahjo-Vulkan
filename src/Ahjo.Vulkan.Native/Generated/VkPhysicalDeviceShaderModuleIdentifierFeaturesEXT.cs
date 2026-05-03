namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderModuleIdentifierFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderModuleIdentifier;
}
