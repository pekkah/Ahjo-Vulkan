using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultInfoEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    public VkDeviceFaultAddressInfoEXT* pAddressInfos;

    public VkDeviceFaultVendorInfoEXT* pVendorInfos;

    public void* pVendorBinaryData;

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
