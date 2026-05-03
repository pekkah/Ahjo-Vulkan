using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkPipelineCacheHeaderVersionOne
{
    [NativeTypeName("uint32_t")]
    public uint headerSize;

    public VkPipelineCacheHeaderVersion headerVersion;

    [NativeTypeName("uint32_t")]
    public uint vendorID;

    [NativeTypeName("uint32_t")]
    public uint deviceID;

    [NativeTypeName("uint8_t[16]")]
    public _pipelineCacheUUID_e__FixedBuffer pipelineCacheUUID;

    [InlineArray(16)]
    public partial struct _pipelineCacheUUID_e__FixedBuffer
    {
        public byte e0;
    }
}
