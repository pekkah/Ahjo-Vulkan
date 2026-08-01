namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangOptimizationLevelIntegral")]
public enum SlangOptimizationLevel : uint
{
    SLANG_OPTIMIZATION_LEVEL_NONE = 0,
    SLANG_OPTIMIZATION_LEVEL_DEFAULT = 1,
    SLANG_OPTIMIZATION_LEVEL_HIGH = 2,
    SLANG_OPTIMIZATION_LEVEL_MAXIMAL = 3,
}
