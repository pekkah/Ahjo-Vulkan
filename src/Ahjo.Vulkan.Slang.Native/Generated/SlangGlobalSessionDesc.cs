using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

public partial struct SlangGlobalSessionDesc
{
    [NativeTypeName("uint32_t")]
    public uint structureSize;

    [NativeTypeName("uint32_t")]
    public uint apiVersion;

    [NativeTypeName("uint32_t")]
    public uint minLanguageVersion;

    public bool enableGLSL;

    [NativeTypeName("uint32_t[16]")]
    public _reserved_e__FixedBuffer reserved;

    [InlineArray(16)]
    public partial struct _reserved_e__FixedBuffer
    {
        public uint e0;
    }
}
