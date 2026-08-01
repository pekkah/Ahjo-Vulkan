using static Ahjo.Vulkan.Slang.Native.SlangModifierID;

namespace Ahjo.Vulkan.Slang.Native;

public partial struct Modifier
{

    [NativeTypeName("SlangModifierIDIntegral")]
    public enum ID : uint
    {
        Shared = SLANG_MODIFIER_SHARED,
        NoDiff = SLANG_MODIFIER_NO_DIFF,
        Static = SLANG_MODIFIER_STATIC,
        Const = SLANG_MODIFIER_CONST,
        Export = SLANG_MODIFIER_EXPORT,
        Extern = SLANG_MODIFIER_EXTERN,
        Differentiable = SLANG_MODIFIER_DIFFERENTIABLE,
        Mutating = SLANG_MODIFIER_MUTATING,
        In = SLANG_MODIFIER_IN,
        Out = SLANG_MODIFIER_OUT,
        InOut = SLANG_MODIFIER_INOUT,
    }
}
