namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One contiguous push-constant range in a Slang program.
/// </summary>
public readonly record struct SlangPushConstantRange
{
    /// <summary>
    /// The name of the <c>[[vk::push_constant]]</c> block parameter this range
    /// came from, or <see cref="string.Empty"/> when Slang reports none.
    /// </summary>
    /// <remarks>
    /// Vulkan never sees it — <c>VkPushConstantRange</c> is three numbers — but
    /// a caller filling the block by member name needs to know which block it
    /// is looking at, and it is the key
    /// <c>SlangReflection.TryGetPushConstantLayout</c> reports back under
    /// <c>SlangBufferLayout.Name</c>.
    /// </remarks>
    public string Name { get; init; }

    /// <summary>The stages that can read this range.</summary>
    public ShaderStages Stages { get; init; }

    /// <summary>Byte offset into the program's push-constant block.</summary>
    public uint Offset { get; init; }

    /// <summary>Byte size of the range.</summary>
    public uint Size { get; init; }
}
