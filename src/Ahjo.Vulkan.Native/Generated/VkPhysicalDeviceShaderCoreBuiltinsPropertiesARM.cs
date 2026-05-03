namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderCoreBuiltinsPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong shaderCoreMask;

    [NativeTypeName("uint32_t")]
    public uint shaderCoreCount;

    [NativeTypeName("uint32_t")]
    public uint shaderWarpsPerCore;
}
