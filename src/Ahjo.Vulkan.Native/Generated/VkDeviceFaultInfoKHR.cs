using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceFaultFlagsKHR")]
    public uint flags;

    [NativeTypeName("uint64_t")]
    public ulong groupId;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    public VkDeviceFaultAddressInfoKHR faultAddressInfo;

    public VkDeviceFaultAddressInfoKHR instructionAddressInfo;

    public VkDeviceFaultVendorInfoKHR vendorInfo;

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
