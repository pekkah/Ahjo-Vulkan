namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderAtomicFloat16VectorFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderFloat16VectorAtomics;
}
