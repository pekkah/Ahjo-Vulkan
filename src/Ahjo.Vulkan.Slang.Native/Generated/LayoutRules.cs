using static Ahjo.Vulkan.Slang.Native.SlangLayoutRules;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangLayoutRulesIntegral")]
public enum LayoutRules : uint
{
    Default = SLANG_LAYOUT_RULES_DEFAULT,
    MetalArgumentBufferTier2 = SLANG_LAYOUT_RULES_METAL_ARGUMENT_BUFFER_TIER_2,
    DefaultStructuredBuffer = SLANG_LAYOUT_RULES_DEFAULT_STRUCTURED_BUFFER,
    DefaultConstantBuffer = SLANG_LAYOUT_RULES_DEFAULT_CONSTANT_BUFFER,
}
