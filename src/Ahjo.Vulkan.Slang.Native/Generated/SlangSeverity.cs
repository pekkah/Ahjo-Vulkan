namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangSeverityIntegral")]
public enum SlangSeverity
{
    SLANG_SEVERITY_DISABLED = 0,
    SLANG_SEVERITY_NOTE = 1,
    SLANG_SEVERITY_WARNING = 2,
    SLANG_SEVERITY_ERROR = 3,
    SLANG_SEVERITY_FATAL = 4,
    SLANG_SEVERITY_INTERNAL = 5,
}
