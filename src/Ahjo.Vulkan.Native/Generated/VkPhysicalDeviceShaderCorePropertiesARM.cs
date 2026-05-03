namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderCorePropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint pixelRate;

    [NativeTypeName("uint32_t")]
    public uint texelRate;

    [NativeTypeName("uint32_t")]
    public uint fmaRate;
}
