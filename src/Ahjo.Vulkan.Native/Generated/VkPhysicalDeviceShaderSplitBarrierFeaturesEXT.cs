namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSplitBarrierFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSplitBarrier;
}
