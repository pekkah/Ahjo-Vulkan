using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Slang.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct GenericArgReflection
{
    [FieldOffset(0)]
    [NativeTypeName("slang::TypeReflection *")]
    public TypeReflection* typeVal;

    [FieldOffset(0)]
    [NativeTypeName("int64_t")]
    public long intVal;

    [FieldOffset(0)]
    public bool boolVal;
}
