namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct CompilerOptionValue
{
    [NativeTypeName("slang::CompilerOptionValueKind")]
    public CompilerOptionValueKind kind;

    [NativeTypeName("int32_t")]
    public int intValue0;

    [NativeTypeName("int32_t")]
    public int intValue1;

    [NativeTypeName("const char *")]
    public sbyte* stringValue0;

    [NativeTypeName("const char *")]
    public sbyte* stringValue1;
}
