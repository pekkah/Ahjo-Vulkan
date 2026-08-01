using static Ahjo.Vulkan.Slang.Native.SlangDeclKind;

namespace Ahjo.Vulkan.Slang.Native;

public partial struct DeclReflection
{

    [NativeTypeName("int")]
    public enum Kind : uint
    {
        Unsupported = SLANG_DECL_KIND_UNSUPPORTED_FOR_REFLECTION,
        Struct = SLANG_DECL_KIND_STRUCT,
        Func = SLANG_DECL_KIND_FUNC,
        Module = SLANG_DECL_KIND_MODULE,
        Generic = SLANG_DECL_KIND_GENERIC,
        Variable = SLANG_DECL_KIND_VARIABLE,
        Namespace = SLANG_DECL_KIND_NAMESPACE,
        Enum = SLANG_DECL_KIND_ENUM,
    }

    public unsafe partial struct IteratedList
    {
        [NativeTypeName("unsigned int")]
        public uint count;

        [NativeTypeName("slang::DeclReflection *")]
        public DeclReflection* parent;

        public unsafe partial struct Iterator
        {
            [NativeTypeName("slang::DeclReflection *")]
            public DeclReflection* parent;

            [NativeTypeName("unsigned int")]
            public uint count;

            [NativeTypeName("unsigned int")]
            public uint index;
        }
    }
}
