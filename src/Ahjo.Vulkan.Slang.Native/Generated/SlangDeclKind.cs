namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangDeclKindIntegral")]
public enum SlangDeclKind : uint
{
    SLANG_DECL_KIND_UNSUPPORTED_FOR_REFLECTION = 0,
    SLANG_DECL_KIND_STRUCT = 1,
    SLANG_DECL_KIND_FUNC = 2,
    SLANG_DECL_KIND_MODULE = 3,
    SLANG_DECL_KIND_GENERIC = 4,
    SLANG_DECL_KIND_VARIABLE = 5,
    SLANG_DECL_KIND_NAMESPACE = 6,
    SLANG_DECL_KIND_ENUM = 7,
}
