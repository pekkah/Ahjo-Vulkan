namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("uint32_t")]
public enum CoverageEntryKind : uint
{
    Unknown = 0,
    Line = 1,
    Branch = 2,
    Function = 3,
    Region = 4,
}
