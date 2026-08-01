namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangScopeIntegral")]
public enum SlangScope : uint
{
    SLANG_SCOPE_NONE,
    SLANG_SCOPE_THREAD,
    SLANG_SCOPE_WAVE,
    SLANG_SCOPE_THREAD_GROUP,
}
