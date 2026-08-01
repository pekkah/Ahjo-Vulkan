namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct PreprocessorMacroDesc
{
    [NativeTypeName("const char *")]
    public sbyte* name;

    [NativeTypeName("const char *")]
    public sbyte* value;
}
