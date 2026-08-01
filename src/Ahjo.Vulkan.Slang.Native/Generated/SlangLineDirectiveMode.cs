namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangLineDirectiveModeIntegral")]
public enum SlangLineDirectiveMode : uint
{
    SLANG_LINE_DIRECTIVE_MODE_DEFAULT = 0,
    SLANG_LINE_DIRECTIVE_MODE_NONE = 1,
    SLANG_LINE_DIRECTIVE_MODE_STANDARD = 2,
    SLANG_LINE_DIRECTIVE_MODE_GLSL = 3,
    SLANG_LINE_DIRECTIVE_MODE_SOURCE_MAP = 4,
}
