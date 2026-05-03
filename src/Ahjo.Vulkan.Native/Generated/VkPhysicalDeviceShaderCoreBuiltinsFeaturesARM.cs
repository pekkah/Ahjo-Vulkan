namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderCoreBuiltinsFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderCoreBuiltins;
}
