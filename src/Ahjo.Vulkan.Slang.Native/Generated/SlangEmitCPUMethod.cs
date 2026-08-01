namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("unsigned int")]
public enum SlangEmitCPUMethod : uint
{
    SLANG_EMIT_CPU_DEFAULT = 0,
    SLANG_EMIT_CPU_VIA_CPP = 1,
    SLANG_EMIT_CPU_VIA_LLVM = 2,
}
