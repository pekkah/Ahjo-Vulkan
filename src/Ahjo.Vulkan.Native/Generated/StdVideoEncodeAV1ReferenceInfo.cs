using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoEncodeAV1ReferenceInfo
{
    public StdVideoEncodeAV1ReferenceInfoFlags flags;

    [NativeTypeName("uint32_t")]
    public uint RefFrameId;

    public StdVideoAV1FrameType frame_type;

    [NativeTypeName("uint8_t")]
    public byte OrderHint;

    [NativeTypeName("uint8_t[3]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("const StdVideoEncodeAV1ExtensionHeader *")]
    public StdVideoEncodeAV1ExtensionHeader* pExtensionHeader;

    [InlineArray(3)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }
}
