namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("unsigned int")]
public enum SlangDiagnosticColor : uint
{
    SLANG_DIAGNOSTIC_COLOR_AUTO = 0,
    SLANG_DIAGNOSTIC_COLOR_ALWAYS = 1,
    SLANG_DIAGNOSTIC_COLOR_NEVER = 2,
}
