namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangFloatingPointModeIntegral")]
public enum SlangFloatingPointMode : uint
{
    SLANG_FLOATING_POINT_MODE_DEFAULT = 0,
    SLANG_FLOATING_POINT_MODE_FAST = 1,
    SLANG_FLOATING_POINT_MODE_PRECISE = 2,
}
