namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("unsigned int")]
public enum SlangEmitSpirvMethod : uint
{
    SLANG_EMIT_SPIRV_DEFAULT = 0,
    SLANG_EMIT_SPIRV_VIA_GLSL = 1,
    SLANG_EMIT_SPIRV_DIRECTLY = 2,
}
