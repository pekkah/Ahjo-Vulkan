using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkPipelineCacheHeaderVersionDataGraphQCOM
{
    [NativeTypeName("uint32_t")]
    public uint headerSize;

    public VkPipelineCacheHeaderVersion headerVersion;

    public VkDataGraphModelCacheTypeQCOM cacheType;

    [NativeTypeName("uint32_t")]
    public uint cacheVersion;

    [NativeTypeName("uint32_t[3]")]
    public _toolchainVersion_e__FixedBuffer toolchainVersion;

    [InlineArray(3)]
    public partial struct _toolchainVersion_e__FixedBuffer
    {
        public uint e0;
    }
}
