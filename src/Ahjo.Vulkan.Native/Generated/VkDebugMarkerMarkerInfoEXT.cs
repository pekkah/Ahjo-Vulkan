using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugMarkerMarkerInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const char *")]
    public sbyte* pMarkerName;

    [NativeTypeName("float[4]")]
    public _color_e__FixedBuffer color;

    [InlineArray(4)]
    public partial struct _color_e__FixedBuffer
    {
        public float e0;
    }
}
