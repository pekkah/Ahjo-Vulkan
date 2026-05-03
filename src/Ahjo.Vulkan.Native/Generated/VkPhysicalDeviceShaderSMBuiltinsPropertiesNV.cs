namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderSMBuiltinsPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderSMCount;

    [NativeTypeName("uint32_t")]
    public uint shaderWarpsPerSM;
}
