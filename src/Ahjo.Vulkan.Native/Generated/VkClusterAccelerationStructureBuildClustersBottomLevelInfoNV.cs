namespace Ahjo.Vulkan.Native;

public partial struct VkClusterAccelerationStructureBuildClustersBottomLevelInfoNV
{
    [NativeTypeName("uint32_t")]
    public uint clusterReferencesCount;

    [NativeTypeName("uint32_t")]
    public uint clusterReferencesStride;

    [NativeTypeName("VkDeviceAddress")]
    public ulong clusterReferences;
}
