namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceComputeShaderDerivativesFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint computeDerivativeGroupQuads;

    [NativeTypeName("VkBool32")]
    public uint computeDerivativeGroupLinear;
}
