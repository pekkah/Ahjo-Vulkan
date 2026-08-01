namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangBindableResourceIntegral")]
public enum SlangBindableResourceType
{
    SLANG_NON_BINDABLE = 0,
    SLANG_TEXTURE = 1,
    SLANG_SAMPLER = 2,
    SLANG_UNIFORM_BUFFER = 3,
    SLANG_STORAGE_BUFFER = 4,
}
