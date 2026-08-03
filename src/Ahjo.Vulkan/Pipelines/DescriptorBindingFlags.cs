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

    /// <summary>
    /// The binding's descriptor count is chosen per set at allocation time
    /// rather than fixed by the layout. Allocate such a set through
    /// <c>DescriptorSetPool.Acquire(layout, variableDescriptorCount)</c> —
    /// the one-argument overload gives the binding an effective count of
    /// zero. Requires the device to have enabled
    /// <c>descriptorBindingVariableDescriptorCount</c>: without it the flag
    /// violates
    /// VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014
    /// at <c>vkCreateDescriptorSetLayout</c> — upstream of any pool.
    /// <c>VK_LAYER_KHRONOS_validation</c> reports it; drivers may accept the
    /// layout silently and misbehave later, since a VUID violation is
    /// undefined behaviour rather than a mandated <c>VkResult</c> failure.
    /// Vulkan permits the flag only on the binding with the highest binding
    /// number in the set
    /// (VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004).
    /// </summary>
    VariableDescriptorCount = 0x00000008,
}
