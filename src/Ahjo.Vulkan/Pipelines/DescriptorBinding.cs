using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One binding in a <see cref="DescriptorSetLayout"/>. Maps onto
/// <c>VkDescriptorSetLayoutBinding</c> + the associated entry in
/// <c>VkDescriptorSetLayoutBindingFlagsCreateInfo</c>.
/// </summary>
/// <remarks>
/// <see cref="Count"/> defaults to 1 (the dominant case). Use
/// <see cref="DescriptorBindingFlags.VariableDescriptorCount"/> with a
/// large <see cref="Count"/> for bindless arrays.
/// </remarks>
public readonly record struct DescriptorBinding
{
    public uint                   Slot          { get; init; }
    public VkDescriptorType       Type          { get; init; }
    public uint                   Count         { get; init; }
    public ShaderStages           Stages        { get; init; }
    public DescriptorBindingFlags BindingFlags  { get; init; }
}
