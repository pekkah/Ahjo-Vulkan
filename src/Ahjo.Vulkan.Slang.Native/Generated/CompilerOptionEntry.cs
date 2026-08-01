namespace Ahjo.Vulkan.Slang.Native;

public partial struct CompilerOptionEntry
{
    [NativeTypeName("slang::CompilerOptionName")]
    public CompilerOptionName name;

    [NativeTypeName("slang::CompilerOptionValue")]
    public CompilerOptionValue value;
}
