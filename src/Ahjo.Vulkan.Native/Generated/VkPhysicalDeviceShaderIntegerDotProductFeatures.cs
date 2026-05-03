namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderIntegerDotProductFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderIntegerDotProduct;
}
