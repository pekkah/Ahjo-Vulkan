namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrix2PropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint cooperativeMatrixWorkgroupScopeMaxWorkgroupSize;

    [NativeTypeName("uint32_t")]
    public uint cooperativeMatrixFlexibleDimensionsMaxDimension;

    [NativeTypeName("uint32_t")]
    public uint cooperativeMatrixWorkgroupScopeReservedSharedMemory;
}
