namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangDebugInfoLevelIntegral")]
public enum SlangDebugInfoLevel : uint
{
    SLANG_DEBUG_INFO_LEVEL_NONE = 0,
    SLANG_DEBUG_INFO_LEVEL_MINIMAL = 1,
    SLANG_DEBUG_INFO_LEVEL_STANDARD = 2,
    SLANG_DEBUG_INFO_LEVEL_MAXIMAL = 3,
}
