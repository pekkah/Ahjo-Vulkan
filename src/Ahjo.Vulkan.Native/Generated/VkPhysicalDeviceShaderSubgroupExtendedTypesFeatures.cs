namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSubgroupExtendedTypesFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupExtendedTypes;
}
