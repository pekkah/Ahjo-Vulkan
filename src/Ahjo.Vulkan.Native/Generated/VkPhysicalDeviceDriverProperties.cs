using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDriverProperties
{
    public VkStructureType sType;

    public void* pNext;

    public VkDriverId driverID;

    [NativeTypeName("char[256]")]
    public _driverName_e__FixedBuffer driverName;

    [NativeTypeName("char[256]")]
    public _driverInfo_e__FixedBuffer driverInfo;

    public VkConformanceVersion conformanceVersion;

    [InlineArray(256)]
    public partial struct _driverName_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _driverInfo_e__FixedBuffer
    {
        public sbyte e0;
    }
}
