using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Slang.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct SlangReflectionGenericArg
{
    [FieldOffset(0)]
    public SlangReflectionType* typeVal;

    [FieldOffset(0)]
    [NativeTypeName("int64_t")]
    public long intVal;

    [FieldOffset(0)]
    public bool boolVal;
}
