using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultInfoEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    public VkDeviceFaultAddressInfoKHR* pAddressInfos;

    public VkDeviceFaultVendorInfoKHR* pVendorInfos;

    public void* pVendorBinaryData;

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
