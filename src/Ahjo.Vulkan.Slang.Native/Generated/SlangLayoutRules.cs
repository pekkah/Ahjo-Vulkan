namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangLayoutRulesIntegral")]
public enum SlangLayoutRules : uint
{
    SLANG_LAYOUT_RULES_DEFAULT = 0,
    SLANG_LAYOUT_RULES_METAL_ARGUMENT_BUFFER_TIER_2 = 1,
    SLANG_LAYOUT_RULES_DEFAULT_STRUCTURED_BUFFER = 2,
    SLANG_LAYOUT_RULES_DEFAULT_CONSTANT_BUFFER = 3,
}
