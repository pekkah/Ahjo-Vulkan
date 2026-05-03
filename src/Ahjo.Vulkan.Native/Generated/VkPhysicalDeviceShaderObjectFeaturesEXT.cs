namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderObjectFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderObject;
}
