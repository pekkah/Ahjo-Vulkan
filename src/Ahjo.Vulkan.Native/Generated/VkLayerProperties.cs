using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkLayerProperties
{
    [NativeTypeName("char[256]")]
    public _layerName_e__FixedBuffer layerName;

    [NativeTypeName("uint32_t")]
    public uint specVersion;

    [NativeTypeName("uint32_t")]
    public uint implementationVersion;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [InlineArray(256)]
    public partial struct _layerName_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
