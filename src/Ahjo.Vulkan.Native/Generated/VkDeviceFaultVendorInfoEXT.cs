using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceFaultVendorInfoEXT
{
    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [NativeTypeName("uint64_t")]
    public ulong vendorFaultCode;

    [NativeTypeName("uint64_t")]
    public ulong vendorFaultData;

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
