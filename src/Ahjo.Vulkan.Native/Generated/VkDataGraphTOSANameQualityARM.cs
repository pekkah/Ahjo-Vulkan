using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkDataGraphTOSANameQualityARM
{
    [NativeTypeName("char[128]")]
    public _name_e__FixedBuffer name;

    [NativeTypeName("VkDataGraphTOSAQualityFlagsARM")]
    public uint qualityFlags;

    [InlineArray(128)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }
}
