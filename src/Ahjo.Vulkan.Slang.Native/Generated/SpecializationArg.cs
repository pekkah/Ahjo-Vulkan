using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct SpecializationArg
{
    [NativeTypeName("slang::SpecializationArg::Kind")]
    public Kind kind;

    [NativeTypeName("__AnonymousRecord_slang_L5671_C5")]
    public _Anonymous_e__Union Anonymous;

    [UnscopedRef]
    public ref TypeReflection* type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref Anonymous.type;
        }
    }

    [UnscopedRef]
    public ref sbyte* expr
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref Anonymous.expr;
        }
    }

    [NativeTypeName("int32_t")]
    public enum Kind : uint
    {
        Unknown = 0,
        Type = 1,
        Expr = 2,
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("slang::TypeReflection *")]
        public TypeReflection* type;

        [FieldOffset(0)]
        [NativeTypeName("const char *")]
        public sbyte* expr;
    }
}
