namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkDescriptorBindingFlagBits</c> (Vulkan 1.4
/// core). Drives the bindless / variable-count / partially-bound paths
/// that real engine code needs and the C++ headers leave as raw uint
/// flags.
/// </summary>
[Flags]
public enum DescriptorBindingFlags : uint
{
    None                   = 0,
    UpdateAfterBind        = 0x00000001,
    UpdateUnusedWhilePending = 0x00000002,
    PartiallyBound         = 0x00000004,
    VariableDescriptorCount = 0x00000008,
}
