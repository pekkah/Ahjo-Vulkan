using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One binding in a <see cref="DescriptorSetLayout"/>. Maps onto
/// <c>VkDescriptorSetLayoutBinding</c> + the associated entry in
/// <c>VkDescriptorSetLayoutBindingFlagsCreateInfo</c>.
/// </summary>
/// <remarks>
/// <para><see cref="Count"/> defaults to 1 (the dominant case). Use
/// <see cref="DescriptorBindingFlags.VariableDescriptorCount"/> with a
/// large <see cref="Count"/> for bindless arrays. <see cref="Count"/> is
/// then the <i>maximum</i>: the count this particular set holds in that
/// binding is chosen at
/// <c>DescriptorSetPool.Acquire(layout, variableDescriptorCount)</c> and
/// must not exceed it
/// (VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380).</para>
/// <para><b>Valid-by-default (issue #119):</b> the <see cref="Count"/> field
/// initializer makes <c>new DescriptorBinding { … }</c> a single-descriptor
/// binding without the caller restating <c>Count = 1</c>. Note that a
/// <c>default(DescriptorBinding)</c> element inside a
/// <see cref="ReadOnlySpan{T}"/> bypasses the initializer (it is zeroed, not
/// constructed) — the layout/template build paths keep a cheap
/// <c>Count == 0 ? 1</c> normalization for that case.</para>
/// </remarks>
public readonly record struct DescriptorBinding
{
    public uint                   Slot          { get; init; }
    public VkDescriptorType       Type          { get; init; }
    public uint                   Count         { get; init; } = 1;
    public ShaderStages           Stages        { get; init; }
    public DescriptorBindingFlags BindingFlags  { get; init; }

    /// <summary>
    /// Runs the <see cref="Count"/> = 1 field initializer (issue #119) —
    /// required explicitly for a struct with field initializers (CS8983).
    /// </summary>
    public DescriptorBinding() { }
}
