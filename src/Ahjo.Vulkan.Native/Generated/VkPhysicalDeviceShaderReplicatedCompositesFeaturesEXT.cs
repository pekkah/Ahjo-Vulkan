namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderReplicatedCompositesFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderReplicatedComposites;
}
