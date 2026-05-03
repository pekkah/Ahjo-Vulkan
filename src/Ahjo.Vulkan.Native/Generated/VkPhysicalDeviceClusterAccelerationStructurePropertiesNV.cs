namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceClusterAccelerationStructurePropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxVerticesPerCluster;

    [NativeTypeName("uint32_t")]
    public uint maxTrianglesPerCluster;

    [NativeTypeName("uint32_t")]
    public uint clusterScratchByteAlignment;

    [NativeTypeName("uint32_t")]
    public uint clusterByteAlignment;

    [NativeTypeName("uint32_t")]
    public uint clusterTemplateByteAlignment;

    [NativeTypeName("uint32_t")]
    public uint clusterBottomLevelByteAlignment;

    [NativeTypeName("uint32_t")]
    public uint clusterTemplateBoundsByteAlignment;

    [NativeTypeName("uint32_t")]
    public uint maxClusterGeometryIndex;
}
