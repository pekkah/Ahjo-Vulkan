using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorRollingBackingCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t[4]")]
    public _wraps_e__FixedBuffer wraps;

    [InlineArray(4)]
    public partial struct _wraps_e__FixedBuffer
    {
        public uint e0;
    }
}
