using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceCounterDescriptionKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPerformanceCounterDescriptionFlagsKHR")]
    public uint flags;

    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    [NativeTypeName("char[256]")]
    public _category_e__FixedBuffer category;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [InlineArray(256)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _category_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
