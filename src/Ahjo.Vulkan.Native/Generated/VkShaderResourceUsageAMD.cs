namespace Ahjo.Vulkan.Native;

public partial struct VkShaderResourceUsageAMD
{
    [NativeTypeName("uint32_t")]
    public uint numUsedVgprs;

    [NativeTypeName("uint32_t")]
    public uint numUsedSgprs;

    [NativeTypeName("uint32_t")]
    public uint ldsSizePerLocalWorkGroup;

    [NativeTypeName("size_t")]
    public nuint ldsUsageSizeInBytes;

    [NativeTypeName("size_t")]
    public nuint scratchMemUsageInBytes;
}
