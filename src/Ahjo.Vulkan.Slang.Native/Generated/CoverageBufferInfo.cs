namespace Ahjo.Vulkan.Slang.Native;

public partial struct CoverageBufferInfo
{
    [NativeTypeName("size_t")]
    public nuint structSize;

    [NativeTypeName("int32_t")]
    public int space;

    [NativeTypeName("int32_t")]
    public int binding;

    [NativeTypeName("uint32_t")]
    public uint elementByteWidth;
}
