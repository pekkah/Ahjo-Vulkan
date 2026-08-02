namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One contiguous push-constant range in a Slang program.
/// </summary>
public readonly record struct SlangPushConstantRange
{
    public ShaderStages Stages { get; init; }
    public uint Offset { get; init; }
    public uint Size { get; init; }
}
