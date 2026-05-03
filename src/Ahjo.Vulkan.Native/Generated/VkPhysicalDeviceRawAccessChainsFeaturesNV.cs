namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRawAccessChainsFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderRawAccessChains;
}
