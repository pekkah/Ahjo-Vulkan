namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSubgroupPartitionedFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupPartitioned;
}
