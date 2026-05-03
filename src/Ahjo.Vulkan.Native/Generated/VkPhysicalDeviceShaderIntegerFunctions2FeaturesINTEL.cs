namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderIntegerFunctions2FeaturesINTEL
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderIntegerFunctions2;
}
