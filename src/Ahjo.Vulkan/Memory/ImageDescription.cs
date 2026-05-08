using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Allocator.CreateImage"/>. Maps onto
/// <c>VkImageCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, queue-sharing fields).
/// </summary>
/// <remarks>
/// Vulkan-native enums (<c>VkFormat</c>, <c>VkImageType</c>,
/// <c>VkImageTiling</c>, <c>VkImageLayout</c>, <c>VkSampleCountFlagBits</c>)
/// pass through from the bindings; only the bit-field types
/// (<see cref="ImageUsage"/>) are shadowed because flag enums benefit most
/// from <c>[Flags]</c> + IDE type-help.
/// </remarks>
public readonly record struct ImageDescription
{
    public VkImageType            ImageType     { get; init; }
    public VkFormat               Format        { get; init; }
    public uint                   Width         { get; init; }
    public uint                   Height        { get; init; }
    public uint                   Depth         { get; init; }
    public uint                   MipLevels     { get; init; }
    public uint                   ArrayLayers   { get; init; }
    public VkSampleCountFlagBits  Samples       { get; init; }
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
}
