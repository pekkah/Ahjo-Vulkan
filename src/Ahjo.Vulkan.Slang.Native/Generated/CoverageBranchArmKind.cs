namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("uint32_t")]
public enum CoverageBranchArmKind : uint
{
    Unknown = 0,
    TrueArm = 1,
    FalseArm = 2,
    CaseArm = 3,
    DefaultArm = 4,
}
