namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceGpaPropertiesAMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPhysicalDeviceGpaPropertiesFlagsAMD")]
    public uint flags;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxSqttSeBufferSize;

    [NativeTypeName("uint32_t")]
    public uint shaderEngineCount;

    [NativeTypeName("uint32_t")]
    public uint perfBlockCount;

    public VkGpaPerfBlockPropertiesAMD* pPerfBlocks;
}
