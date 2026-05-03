namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderCoreProperties2AMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkShaderCorePropertiesFlagsAMD")]
    public uint shaderCoreFeatures;

    [NativeTypeName("uint32_t")]
    public uint activeComputeUnitCount;
}
