namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSubgroupUniformControlFlowFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupUniformControlFlow;
}
