using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Allocator.CreateImage"/>. Maps onto
/// <c>VkImageCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, queue-sharing fields).
/// </summary>
/// <remarks>
/// <para>Vulkan-native enums (<c>VkFormat</c>, <c>VkImageType</c>,
/// <c>VkImageTiling</c>, <c>VkImageLayout</c>, <c>VkSampleCountFlagBits</c>)
/// pass through from the bindings; only the bit-field types
/// (<see cref="ImageUsage"/>) are shadowed because flag enums benefit most
/// from <c>[Flags]</c> + IDE type-help.</para>
/// <para><b>Valid-by-default (issue #119):</b> <c>new ImageDescription { … }</c>
/// is a valid create-info on its own. The dimension/sample/type fields default
/// to the overwhelmingly common single-mip, single-layer, non-multisampled 2D
/// image, so callers only set <see cref="Format"/>, <see cref="Width"/>,
/// <see cref="Height"/>, and <see cref="Usage"/> for the typical case. A
/// zero-default here used to produce an *invalid* <c>VkImageCreateInfo</c>
/// (<c>mipLevels = 0</c>, <c>samples = 0</c>); the field initializers fix that
/// at the type level rather than via call-site normalization.</para>
/// </remarks>
public readonly record struct ImageDescription
{
    public VkImageType            ImageType     { get; init; } = VkImageType.VK_IMAGE_TYPE_2D;
    public VkFormat               Format        { get; init; }
    public uint                   Width         { get; init; }
    public uint                   Height        { get; init; }
    public uint                   Depth         { get; init; } = 1;
    public uint                   MipLevels     { get; init; } = 1;
    public uint                   ArrayLayers   { get; init; } = 1;
    public VkSampleCountFlagBits  Samples       { get; init; } = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
    public VkImageTiling          Tiling        { get; init; }
    public ImageUsage             Usage         { get; init; }
    public VkImageLayout          InitialLayout { get; init; }

    /// <summary>
    /// <c>VkImageCreateInfo.flags</c>. Default zero covers ordinary 1D/2D/3D
    /// images. Set <c>VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT</c> on a 2D image
    /// with <see cref="ArrayLayers"/> = 6 to allow <c>VkImageViewType.Cube</c>
    /// (and <c>CubeArray</c> on layer multiples of 6) views; without the
    /// flag the cube view <c>vkCreateImageView</c> call fails. Bind the
    /// native bitfield enum directly — the bindings expose this field as
    /// the <c>uint</c>-typedef'd <c>VkImageCreateFlags</c>, and combining
    /// bits with <c>|</c> on the enum stays inside the enum's type.
    /// </summary>
    public VkImageCreateFlagBits Flags { get; init; }

    /// <summary>
    /// Runs the valid-by-default field initializers (issue #119). C# requires
    /// a struct with field initializers to declare a constructor explicitly
    /// (CS8983); this is what makes <c>new ImageDescription()</c> /
    /// <c>new ImageDescription { … }</c> start from the 2D, single-mip,
    /// single-layer, single-sample baseline.
    /// </summary>
    public ImageDescription() { }
}
