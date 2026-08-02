using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One binding in a Slang descriptor set layout.
/// </summary>
/// <remarks>
/// <b><see cref="Count"/> is an option, not a number.</b> An unbounded
/// (bindless) array has no descriptor count Slang can state, and no
/// <see cref="uint"/> is safe to put here — <c>0</c> is normalized to <c>1</c>
/// by the descriptor-set-layout build path and <c>uint.MaxValue</c> crashes the
/// driver. Read it with <see cref="SlangDescriptorCount.TryGetValue"/>, or map
/// the binding with
/// <c>SlangVulkanMapping.MapBinding(binding, descriptorCount)</c>.
/// </remarks>
public readonly record struct SlangDescriptorBinding
{
    public uint Slot { get; init; }
    public SlangBindingType Type { get; init; }
    public SlangDescriptorCount Count { get; init; }
    public ShaderStages Stages { get; init; }

    public SlangDescriptorBinding()
    {
        Count = SlangDescriptorCount.Fixed(1);
    }
}
