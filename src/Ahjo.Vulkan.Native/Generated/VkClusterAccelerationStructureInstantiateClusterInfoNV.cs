using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkClusterAccelerationStructureInstantiateClusterInfoNV
{
    [NativeTypeName("uint32_t")]
    public uint clusterIdOffset;

    public uint _bitfield;

    [NativeTypeName("uint32_t : 24")]
    public uint geometryIndexOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return _bitfield & 0xFFFFFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~0xFFFFFFu) | (value & 0xFFFFFFu);
        }
    }

    [NativeTypeName("uint32_t : 8")]
    public uint reserved
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return (_bitfield >> 24) & 0xFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~(0xFFu << 24)) | ((value & 0xFFu) << 24);
        }
    }

    [NativeTypeName("VkDeviceAddress")]
    public ulong clusterTemplateAddress;

    public VkStridedDeviceAddressNV vertexBuffer;
}
