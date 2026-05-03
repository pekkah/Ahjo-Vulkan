using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLayeredApiPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint vendorID;

    [NativeTypeName("uint32_t")]
    public uint deviceID;

    public VkPhysicalDeviceLayeredApiKHR layeredAPI;

    [NativeTypeName("char[256]")]
    public _deviceName_e__FixedBuffer deviceName;

    [InlineArray(256)]
    public partial struct _deviceName_e__FixedBuffer
    {
        public sbyte e0;
    }
}
