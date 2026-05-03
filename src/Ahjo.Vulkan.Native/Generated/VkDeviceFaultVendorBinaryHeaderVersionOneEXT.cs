using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceFaultVendorBinaryHeaderVersionOneEXT
{
    [NativeTypeName("uint32_t")]
    public uint headerSize;

    public VkDeviceFaultVendorBinaryHeaderVersionEXT headerVersion;

    [NativeTypeName("uint32_t")]
    public uint vendorID;

    [NativeTypeName("uint32_t")]
    public uint deviceID;

    [NativeTypeName("uint32_t")]
    public uint driverVersion;

    [NativeTypeName("uint8_t[16]")]
    public _pipelineCacheUUID_e__FixedBuffer pipelineCacheUUID;

    [NativeTypeName("uint32_t")]
    public uint applicationNameOffset;

    [NativeTypeName("uint32_t")]
    public uint applicationVersion;

    [NativeTypeName("uint32_t")]
    public uint engineNameOffset;

    [NativeTypeName("uint32_t")]
    public uint engineVersion;

    [NativeTypeName("uint32_t")]
    public uint apiVersion;

    [InlineArray(16)]
    public partial struct _pipelineCacheUUID_e__FixedBuffer
    {
        public byte e0;
    }
}
