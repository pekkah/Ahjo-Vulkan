using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceCounterDescriptionARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPerformanceCounterDescriptionFlagsARM")]
    public uint flags;

    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    [InlineArray(256)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }
}
