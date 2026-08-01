using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct VMExecOperand
{
    [NativeTypeName("uint8_t **")]
    public byte** section;

    public uint _bitfield;

    [NativeTypeName("uint32_t : 8")]
    public uint type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return _bitfield & 0xFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~0xFFu) | (value & 0xFFu);
        }
    }

    [NativeTypeName("uint32_t : 24")]
    public uint size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return (_bitfield >> 8) & 0xFFFFFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _bitfield = (_bitfield & ~(0xFFFFFFu << 8)) | ((value & 0xFFFFFFu) << 8);
        }
    }

    [NativeTypeName("uint32_t")]
    public uint offset;
}
