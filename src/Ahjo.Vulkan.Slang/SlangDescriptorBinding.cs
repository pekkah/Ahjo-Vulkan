using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One binding in a Slang descriptor set layout.
/// </summary>
public readonly record struct SlangDescriptorBinding
{
    public uint Slot { get; init; }
    public SlangBindingType Type { get; init; }
    public uint Count { get; init; }
    public ShaderStages Stages { get; init; }

    public SlangDescriptorBinding()
    {
        Count = 1;
    }
}
