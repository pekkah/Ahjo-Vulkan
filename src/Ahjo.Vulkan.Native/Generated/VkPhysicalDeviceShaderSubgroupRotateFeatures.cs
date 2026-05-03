namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSubgroupRotateFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupRotate;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupRotateClustered;
}
