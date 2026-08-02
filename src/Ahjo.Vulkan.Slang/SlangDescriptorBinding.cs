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
    /// <summary>The Vulkan binding number within its descriptor set.</summary>
    public uint Slot { get; init; }

    /// <summary>
    /// The name the shader declares this binding under —
    /// <c>gAlbedo</c>, <c>gMaterial</c> — or <see cref="string.Empty"/> when
    /// Slang reports none.
    /// </summary>
    /// <remarks>
    /// <para>This is what a material system keys on instead of hard-coding slot
    /// numbers. It is the <em>declared</em> name, not necessarily the
    /// <c>OpName</c> in the emitted SPIR-V: a resource declared inside a
    /// <c>ParameterBlock</c> is named <c>maps</c> here and
    /// <c>gWith.maps</c> there.</para>
    /// <para>The implicit uniform buffer a <c>ParameterBlock</c> owns at binding
    /// 0 of its space has no name of its own — Slang reports no descriptor range
    /// for it at all — so it takes the block parameter's name.</para>
    /// </remarks>
    public string Name { get; init; }

    /// <summary>The Slang binding type. Map it with <c>SlangVulkanMapping.MapBindingType</c>.</summary>
    public SlangBindingType Type { get; init; }

    /// <summary>How many descriptors this binding holds, when Slang can say.</summary>
    public SlangDescriptorCount Count { get; init; }

    /// <summary>The stages that reach this binding.</summary>
    public ShaderStages Stages { get; init; }

    /// <summary>
    /// The format a storage image was declared with —
    /// <c>[[vk::image_format("rgba8")]]</c> — or
    /// <c>SLANG_IMAGE_FORMAT_unknown</c> for everything else.
    /// </summary>
    /// <remarks>
    /// <para><b><c>SLANG_IMAGE_FORMAT_unknown</c> is not a benign default — it
    /// is a device requirement.</b> It means the shader reads or writes that
    /// storage image without stating a format, and Vulkan only permits that
    /// when the capability is there. Below Vulkan 1.3 without
    /// <c>VK_KHR_format_feature_flags2</c>, an <c>Unknown</c>-format storage
    /// image must be <c>NonReadable</c> / <c>NonWritable</c> unless
    /// <c>shaderStorageImageReadWithoutFormat</c> /
    /// <c>shaderStorageImageWriteWithoutFormat</c> is enabled
    /// (<c>VUID-RuntimeSpirv-apiVersion-07955</c> / <c>-07954</c>). From 1.3 on,
    /// the check moves to draw/dispatch time and lands on the bound
    /// <c>VkImageView</c>'s format features —
    /// <c>VK_FORMAT_FEATURE_2_STORAGE_READ_WITHOUT_FORMAT_BIT</c> /
    /// <c>..._WRITE_WITHOUT_FORMAT_BIT</c>
    /// (<c>VUID-vkCmdDispatch-OpTypeImage-07028</c> / <c>-07027</c>).</para>
    /// <para>A <em>non</em>-<c>unknown</c> value is the other half of the
    /// contract: the <c>VkImageView</c> bound there must have a matching
    /// format.</para>
    /// <para>Reflection reports Slang's own enum. There is deliberately no
    /// <c>VkFormat</c> mapping in <c>SlangVulkanMapping</c> yet: no Vulkan call
    /// at descriptor-set-layout creation time takes a storage image's format,
    /// so the ~40-entry table would be surface with no consumer. The value
    /// matters when the caller creates the <c>VkImageView</c> the binding is
    /// written with, and that is the caller's call.</para>
    /// </remarks>
    public SlangImageFormat ImageFormat { get; init; }

    /// <summary>
    /// <see langword="true"/> when this binding's type is existential or
    /// generic — the binding whose concrete type a type conformance supplies.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c> / win-x64: a
    /// <c>ParameterBlock&lt;ISurface&gt;</c> reports <see langword="true"/>;
    /// every concrete binding in every other fixture reports
    /// <see langword="false"/>. For a <c>ParameterBlock</c> the flag is Slang's
    /// answer about the <em>block</em>, and it lands on the block's implicit
    /// uniform buffer at binding 0 — the only binding that block has.
    /// </remarks>
    public bool IsSpecializable { get; init; }

    public SlangDescriptorBinding()
    {
        Count = SlangDescriptorCount.Fixed(1);
        Name = string.Empty;
    }
}
