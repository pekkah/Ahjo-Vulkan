using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkClusterAccelerationStructureGeometryIndexAndGeometryFlagsNV
{
    public uint _bitfield;

    [NativeTypeName("uint32_t : 24")]
    public uint geometryIndex
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

    [NativeTypeName("uint32_t : 5")]
    public uint reserved
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return (_bitfield >> 24) & 0x1Fu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~(0x1Fu << 24)) | ((value & 0x1Fu) << 24);
        }
    }

    [NativeTypeName("uint32_t : 3")]
    public uint geometryFlags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return (_bitfield >> 29) & 0x7u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~(0x7u << 29)) | ((value & 0x7u) << 29);
        }
    }
}
