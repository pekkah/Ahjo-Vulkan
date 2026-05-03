using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceToolProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    [NativeTypeName("char[256]")]
    public _version_e__FixedBuffer version;

    [NativeTypeName("VkToolPurposeFlags")]
    public uint purposes;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [NativeTypeName("char[256]")]
    public _layer_e__FixedBuffer layer;

    [InlineArray(256)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _version_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _layer_e__FixedBuffer
    {
        public sbyte e0;
    }
}
