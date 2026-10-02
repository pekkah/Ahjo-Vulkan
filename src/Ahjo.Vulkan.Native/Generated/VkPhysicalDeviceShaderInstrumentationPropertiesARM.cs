namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderInstrumentationPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint numMetrics;

    [NativeTypeName("VkBool32")]
    public uint perBasicBlockGranularity;
}
